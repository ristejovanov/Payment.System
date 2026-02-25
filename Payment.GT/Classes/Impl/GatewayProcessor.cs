using Microsoft.Extensions.Logging;
using Payment.GT.Classes.Interface;
using Payment.Protocol;
using Payment.Protocol.Dto;
using Payment.Protocol.Dtos;
using Payment.Protocol.Impl.Base;
using Payment.Protocol.Interface;
using Payment.Shared.Dto;
using Payment.Shared.Enums;
using System.Globalization;

namespace Payment.GT.Classes
{
    public sealed class GatewayProcessor
    {
        private readonly IGatewayStateStore _store;
        private readonly IIssuerClient _issuer;
        private readonly ILogger<GatewayProcessor> _log;
        private readonly IFrameOperator _frameOperator;
        private readonly ITlvMapper _mapper;
        private readonly IMessageValidator<A70RequestDto> _a70Validator;
        private readonly IMessageValidator<A72RequestDto> _a72Validator;


        public GatewayProcessor(IGatewayStateStore store
            , IIssuerClient issuer
            , IFrameOperator frameOperator
            , ILogger<GatewayProcessor> log
            , IMessageValidator<A70RequestDto> a70Validator
            , IMessageValidator<A72RequestDto> a72Validator)
        {
            _store = store;
            _issuer = issuer;
            _log = log;
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
                0x01 => HandleHeartbeat(req), // if you define heartbeat msgType
                _ => BuildErrorResponse(req, "96", "unsuported msg type")
            };
        }

        private async Task<byte[]> HandleA70Async(Frame req, CancellationToken ct)
        {
            // get the data and map it to a Dto ---------------------------------------
            A70RequestDto dto;
            try
            {
                dto = _mapper.FromTlvs<A70RequestDto>(req.Tlvs);
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "A70 TLV->DTO mapping failed");
                return BuildErrorResponse(req, "96", "Bad TLV mapping");
            }

            // validate the Dto -------------------------------------------------------
            var validation = _a70Validator.Validate(dto);
            if (!validation.IsValid)
            {
                _log.LogInformation("A70 validation failed: {Errors}", validation.ToString());
                return BuildA71(dto.AtmId ?? "", dto.Stan.ToString(CultureInfo.InvariantCulture), dto.CorrelationId ?? "",
                    rc: "96",
                    authCode: null,
                    msg: "Validation failed:" + validation.ToString());
            }

            var dedupeKey = $"A70:{dto.AtmId}:{dto.Stan}";
            var isRepeat = _store.TryGetResponse(dedupeKey, out var cached);

            using var scope = _log.BeginScope(new Dictionary<string, object?>
            {
                ["atmId"] = dto.AtmId,
                ["stan"] = dto.Stan,
                ["correlationId"] = dto.CorrelationId,
                ["isRepeat"] = isRepeat
            });

            if (isRepeat)
            {
                _log.LogInformation("A70 dedup hit; returning cached A71");
                return cached!;
            }

            IssuerDecision decision;
            try
            {
                using var issuerCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                issuerCts.CancelAfter(TimeSpan.FromSeconds(2));
                decision = await _issuer.AuthorizeAsync(dto.Pan, dto.PinBlock, dto.AmountMinor, dto.Currency, issuerCts.Token);
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

            var status = decision.Rc == "00" ? ReservationStatus.Approved :
                         decision.Rc == "91" ? ReservationStatus.Timeout :
                         ReservationStatus.Declined;

            _store.UpsertReservation(new ReservationRecord(
                AtmId: dto.AtmId,
                Stan: dto.Stan,
                CorrelationId: dto.CorrelationId,
                Status: status,
                Rc: decision.Rc,
                AuthCode: decision.AuthCode,
                Message: decision.Message,
                CreatedUtc: DateTimeOffset.UtcNow
            ));

            var resp = BuildA71(dto.AtmId, dto.Stan.ToString(CultureInfo.InvariantCulture), dto.CorrelationId,
                decision.Rc, decision.AuthCode, decision.Message);

            _store.StoreResponse(dedupeKey, resp);
            return resp;
        }

        private async Task<byte[]> HandleA72Async(Frame req, CancellationToken ct)
        {
            var atmId = req.GetAsciiOrNull(Tags.AtmId) ?? "";
            var stanStr = req.GetAsciiOrNull(Tags.Stan) ?? "";     // completion message stan
            var corr = req.GetAsciiOrNull(Tags.CorrelationId) ?? "";
            var originalStanStr = req.GetAsciiOrNull(Tags.OriginalStan) ?? "";

            if (string.IsNullOrWhiteSpace(atmId) || string.IsNullOrWhiteSpace(originalStanStr) || string.IsNullOrWhiteSpace(corr))
                return BuildA73(atmId, stanStr, corr, "96", "MISSING_FIELDS");

            if (!long.TryParse(originalStanStr, out var originalStan))
                return BuildA73(atmId, stanStr, corr, "96", "BAD_ORIGINAL_STAN");

            using var scope = _log.BeginScope(new Dictionary<string, object?>
            {
                ["atmId"] = atmId,
                ["correlationId"] = corr,
                ["originalStan"] = originalStan,
                ["completionStan"] = stanStr
            });

            // Idempotency: completion repeats should return same A73
            var dedupeKey = $"A72:{atmId}:{originalStan}";
            if (_store.TryGetResponse(dedupeKey, out var cached))
            {
                _log.LogInformation("A72 dedup hit; returning cached A73");
                return cached;
            }

            // Must reference existing approved reservation
            if (!_store.TryGetReservationByStan(atmId, originalStan, out var res) || res.Status != ReservationStatus.Approved)
            {
                var notFound = BuildA73(atmId, stanStr, corr, "25", "NOT_FOUND");
                _store.StoreResponse(dedupeKey, notFound);
                return notFound;
            }

            // In real world: finalize debit / reversal based on dispenseResult + dispensedAmountMinor.
            // Here we just accept and respond.
            var ok = BuildA73(atmId, stanStr, corr, "00", "COMPLETED");
            _store.StoreResponse(dedupeKey, ok);
            return ok;
        }

        private byte[] CacheAndReturn(string key, byte[] resp) => resp;

        private byte[] BuildA71(string atmId, string stan, string corr, string rc, string? authCode, string? msg)
        {
            // 4) Build TLVs for A70
            var tlvs = new List<Tlv>
            {
                Tlv.Ascii(Tags.AtmId, atmId),
                Tlv.Ascii(Tags.Stan, stan.ToString(CultureInfo.InvariantCulture)),
                Tlv.Ascii(Tags.CorrelationId, corr),
                Tlv.Ascii(Tags.Rc, rc),
            };

            if (!string.IsNullOrWhiteSpace(authCode))
                tlvs.Add(Tlv.Ascii(Tags.AuthCode, authCode));
            if (!string.IsNullOrWhiteSpace(msg))
                tlvs.Add(Tlv.Ascii(Tags.Message, msg));

            return _frameOperator.FrameToBinary(new Frame { MsgType = MessageTypes.A71, Tlvs = tlvs });
        }

        private byte[] BuildA73(string atmId, string stan, string corr, string rc, string? msg)
        {

            var tlvs = new List<Tlv>
            {
                Tlv.Ascii(Tags.AtmId, atmId),
                Tlv.Ascii(Tags.Stan, stan.ToString(CultureInfo.InvariantCulture)),
                Tlv.Ascii(Tags.CorrelationId, corr),
                Tlv.Ascii(Tags.Rc, rc),
            };

            if (!string.IsNullOrWhiteSpace(msg))
                tlvs.Add(Tlv.Ascii(Tags.Message, msg));

            return _frameOperator.FrameToBinary(new Frame { MsgType = MessageTypes.A73, Tlvs = tlvs });
        }

        private byte[] BuildErrorResponse(Frame req, string rc, string msg)
        {
            // respond with same correlation/atmId/stan if available; fall back if missing
            var atmId = req.GetAsciiOrNull(Tags.AtmId) ?? "";
            var stan = req.GetAsciiOrNull(Tags.Stan) ?? "";
            var corr = req.GetAsciiOrNull(Tags.CorrelationId) ?? "";
            return req.MsgType == 0x70 ? BuildA71(atmId, stan, corr, rc, null, msg)
                 : BuildA73(atmId, stan, corr, rc, msg);
        }

        private byte[] HandleHeartbeat(Frame req)
        {
            // If you add heartbeat msgType, respond accordingly.
            return _frameOperator.FrameToBinary(new Frame { MsgType = MessageTypes.Pong, Tlvs = Array.Empty<Tlv>() });
        }
    }

}
