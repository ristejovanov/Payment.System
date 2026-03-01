using Microsoft.Extensions.Logging;
using Payment.GT.Classes.Impl;
using Payment.GT.Classes.Interface;
using Payment.Protocol;
using Payment.Protocol.Dto;
using Payment.Protocol.Dtos;
using Payment.Protocol.Interface;
using Payment.Shared.Dto;
using Payment.Shared.Enums;
using System.Collections.Concurrent;
using System.Globalization;

namespace Payment.GT.Classes
{
    public sealed class GatewayProcessor
    {
        private static readonly TimeSpan IssuerTimeout = TimeSpan.FromSeconds(2);

        private readonly IGatewayStateStore _store;
        private readonly IIssuerClient _issuer;
        private readonly ILogger<GatewayProcessor> _log;
        private readonly IObjectCreator _objectCreator;
        private readonly IFrameOperator _frameOperator;
        private readonly ITlvMapper _mapper;
        private readonly IMessageValidator<A70RequestDto> _a70Validator;
        private readonly IMessageValidator<A72RequestDto> _a72Validator;

        // Single-flight per (atmId, stan) to avoid multiple issuer calls while in-flight
        private readonly ConcurrentDictionary<string, Lazy<Task<byte[]>>> _inflightA70 = new(StringComparer.Ordinal);


        public GatewayProcessor(IGatewayStateStore store
            , IIssuerClient issuer
            , IObjectCreator objectCreator
            , IFrameOperator frameOperator
            , ILogger<GatewayProcessor> log
            , IMessageValidator<A70RequestDto> a70Validator
            , IMessageValidator<A72RequestDto> a72Validator)
        {
            _store = store;
            _issuer = issuer;
            _log = log;
            _objectCreator = objectCreator;
            _frameOperator = frameOperator;
            _a70Validator = a70Validator;
            _a72Validator = a72Validator;
        }

        public async Task<byte[]> HandleAsync(Frame req, CancellationToken ct)
        {
            return req.MsgType switch
            {
                0x70 => await HandleA70Async(req, ct),
                0x72 => await HandleA72Async(req, ct),
                0x01 => HandleHeartbeat(), 
                _ => BuildErrorResponse(req, "96", "unsuported msg type")
            };
        }

        private async Task<byte[]> HandleA70Async(Frame req, CancellationToken ct)
        {
            // Get request data and map it to a Dto ---------------------------------------
            A70RequestDto requestDto;
            try
            {
                requestDto = _mapper.FromTlvs<A70RequestDto>(req.Tlvs);
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "A70 TLV->DTO mapping failed");
                return BuildErrorResponse(req, "96", "Bad TLV mapping");
            }

            // Prepare response DTO with fields available at this point ---------------------
            var responseDto = new A71ResponseDto
            {
                CorrelationId = requestDto.CorrelationId!,
                AuthCode = null,
            };


            // Validate request Dto -------------------------------------------------------
            var validation = _a70Validator.Validate(requestDto);
            if (!validation.IsValid)
            {
                _log.LogInformation("A70 validation failed: {Errors}", validation.ToString());
                responseDto.Rc = "96";
                responseDto.Message = "Validation failed:" + validation.ToString();

                return _objectCreator.ToBytes(responseDto);
            }

            // Compute fingerprint for idempotency and collision detection (same stan but different payload)
            var fingerprint = RequestFingerprint.ForA70(requestDto.Pan!, requestDto.ExpiryYYMM!, requestDto.PinBlock!, requestDto.AmountMinor, requestDto.Currency!);

            using var scope = _log.BeginScope(new Dictionary<string, object?>
            {
                ["atmId"] = requestDto.AtmId,
                ["stan"] = requestDto.Stan,
                ["correlationId"] = requestDto.CorrelationId,
                ["isRepeat"] = requestDto.IsRepeat
            });


            // Atomic begin at the store layer (first writer wins)
            var begin = _store.BeginReservation(requestDto, fingerprint);

            // The store layer will determine if this is a repeat (same fingerprint) or a collision (different fingerprint) or a new request,
            // and return the appropriate result for each case so we can decide how to respond
            switch (begin.Outcome)
            {
                case BeginReservationOutcome.AlreadyCompleted:
                    _log.LogInformation("A70 replay hit: returning cached A71");
                    return begin.CachedA71Bytes!;

                case BeginReservationOutcome.StanReuseDifferentPayload:
                    {
                        _log.LogWarning("STAN collision with different payload; returning 96");
                     
                        responseDto.Rc = "96";
                        responseDto.Message = begin.ErrorMessage ?? "STAN collision with different payload";
                        return _objectCreator.ToBytes(responseDto);
                    }
                case BeginReservationOutcome.AlreadyProcessing:
                    _log.LogInformation("A70 in-flight join (repeat before issuer completed)");
                    return await JoinOrStartInflightA70(requestDto, fingerprint, ct);

                case BeginReservationOutcome.StartedNew:
                    // leader path: start inflight task (still use single-flight to unify duplicates that arrive immediately after)
                    _log.LogInformation("A70 started new processing");
                    return await JoinOrStartInflightA70(requestDto, fingerprint, ct);

                default:
                    {
                        responseDto.Rc = "96";
                        responseDto.Message = begin.ErrorMessage ?? "internal state error";
                        return _objectCreator.ToBytes(responseDto);
                    }
            }
        }

        private Task<byte[]> JoinOrStartInflightA70(A70RequestDto request, string fingerprint, CancellationToken ct)
        {
            var inflightKey = $"A70:{request.AtmId}:{request.Stan}"; // primary identity: (atmId, stan)

            var lazy = _inflightA70.GetOrAdd(inflightKey, _ => new Lazy<Task<byte[]>>(
                () => ProcessA70OnceAsync(request, fingerprint, ct),
                LazyThreadSafetyMode.ExecutionAndPublication));

            return AwaitAndCleanup(inflightKey, lazy);

            async Task<byte[]> AwaitAndCleanup(string key, Lazy<Task<byte[]>> lz)
            {
                try { return await lz.Value.ConfigureAwait(false); }
                finally { _inflightA70.TryRemove(key, out _); }
            }
        }

        private async Task<byte[]> ProcessA70OnceAsync(A70RequestDto dto, string fingerprint, CancellationToken ct)
        {
            // Issuer call with 2s timeout (spec)
            IssuerDecision decision;
            try
            {
                using var issuerCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                issuerCts.CancelAfter(IssuerTimeout);

                decision = await _issuer.AuthorizeAsync(
                    pan: dto.Pan!,
                    pinBlock: dto.PinBlock!,
                    amountMinor: dto.AmountMinor,
                    currency: dto.Currency!,
                    ct: issuerCts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                decision = new IssuerDecision { Rc = "91", AuthCode = null, Message = "ISSUER_UNAVAILABLE" };
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Issuer error");
                decision = new IssuerDecision { Rc = "96", AuthCode = null, Message = "ISSUER_ERROR" };
            }

            // Build A71 bytes (deterministic) and persist for replay
           var a71Bytes = _objectCreator.ToBytes(new A71ResponseDto 
                    { CorrelationId = dto.CorrelationId!
                    , Rc = decision.Rc
                    , AuthCode = decision.AuthCode
                    , Message = decision.Message });

            _store.CompleteReservation(new CompleteReservationRequest(
                AtmId: dto.AtmId!,
                Stan: dto.Stan,
                CorrelationId: dto.CorrelationId!,
                AmountMinor: dto.AmountMinor,
                Currency: dto.Currency,
                Fingerprint: fingerprint,
                Rc: decision.Rc,
                AuthCode: decision.AuthCode,
                Message: decision.Message,
                A71Bytes: a71Bytes,
                CompletedUtc: DateTimeOffset.UtcNow));

            return a71Bytes;
        }


        // ------------------------------------------------------------
        // A72 Completion: must reference approved reservation; idempotent A73 by (atmId, originalStan)
        // ------------------------------------------------------------
        private Task<byte[]> HandleA72Async(Frame req, CancellationToken ct)
        {
            // get the data and map it to a Dto ---------------------------------------
            A72RequestDto requestDto;
            try
            {
                requestDto = _mapper.FromTlvs<A72RequestDto>(req.Tlvs);
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "A70 TLV->DTO mapping failed");
                return Task.FromResult(BuildErrorResponse(req, "96", "Bad TLV mapping"));
            }

            // Prepare response DTO with fields available at this point ---------------------
            var responseDto = new A73ResponseDto
            {
                CorrelationId = requestDto.CorrelationId!,
                CompletionStatus = CompletionStatus.Failed
            };

            // validate the Dto -------------------------------------------------------
            var validation = _a72Validator.Validate(requestDto);
            if (!validation.IsValid)
            {
                _log.LogInformation("A20 validation failed: {Errors}", validation.ToString());

                responseDto.Rc = "96";
                responseDto.Message = "Validation failed:" + validation.ToString();
                _objectCreator.ToBytes(responseDto); 
            }

            using var scope = _log.BeginScope(new Dictionary<string, object?>
            {
                ["msgType"] = "A72",
                ["atmId"] = requestDto.AtmId,
                ["completionStan"] = requestDto.Stan,
                ["originalStan"] = requestDto.OriginalStan,
                ["correlationId"] = requestDto.CorrelationId,
                ["isRepeat"] = requestDto.IsRepeat
            });

            // A72 idempotency: key by (atmId, originalStan)
            if (_store.TryGetCompletionResponse(requestDto.AtmId!, requestDto.Stan, out var cachedA73))
            {
                _log.LogInformation("A72 replay hit: returning cached A73");
                return Task.FromResult(cachedA73);
            }

            if (!_store.TryGetReservationByStan(requestDto.AtmId!, requestDto.OriginalStan, out var res))
            { 
                responseDto.Rc = "25";
                responseDto.Message = "Reservation not found";
                var notFound = _objectCreator.ToBytes(responseDto);
                _store.StoreCompletionResponse(requestDto.AtmId!, requestDto.OriginalStan, notFound);
                return Task.FromResult(notFound);
            }

            if(res.Status == ReservationStatus.Processing)
            {
                responseDto.Rc = "91";
                responseDto.Message = "Issuer decision pending, cannot complete";
                var pending = _objectCreator.ToBytes(responseDto);
                return Task.FromResult(pending);
            }

            if (res.Status != ReservationStatus.Approved)
            {
                responseDto.Rc = "91";
                responseDto.Message = "Reservation not approved";
                var notApproved = _objectCreator.ToBytes(responseDto);
                _store.StoreCompletionResponse(requestDto.AtmId!, requestDto.OriginalStan, notApproved);
                return Task.FromResult(notApproved);
            }

            responseDto.Rc = "00";
            responseDto.CompletionStatus = CompletionStatus.Completed;
            responseDto.Message = "COMPLETED";

            var ok = _objectCreator.ToBytes(responseDto);
            _store.StoreCompletionResponse(requestDto.AtmId!, requestDto.OriginalStan, ok);
            return Task.FromResult(ok);
        }
  
        private byte[] BuildErrorResponse(Frame req, string rc, string msg)
        {
            // respond with same correlation/atmId/stan if available; fall back if missing
            var atmId = req.GetAsciiOrNull(Tags.AtmId) ?? "";
            var stanStr = req.GetAsciiOrNull(Tags.Stan) ?? "";
            var corr = req.GetAsciiOrNull(Tags.CorrelationId) ?? "";
            long.TryParse(stanStr, NumberStyles.None, CultureInfo.InvariantCulture, out var stan);

            if(req.MsgType == 0x70)
                return _objectCreator.ToBytes(new A71ResponseDto { CorrelationId = corr, Rc = rc, Message = msg });

                return _objectCreator.ToBytes(new A73ResponseDto { CorrelationId = corr, Rc = rc, Message = msg });
            }

        private byte[] HandleHeartbeat()
        {
            // If you add heartbeat msgType, respond accordingly.
            return _frameOperator.FrameToBinary(new Frame { MsgType = MessageTypes.Pong, Tlvs = Array.Empty<Tlv>() });
        }
    }

}
