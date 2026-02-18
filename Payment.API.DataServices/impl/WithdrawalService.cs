using ATM.DataServices.interfaces;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Payment.API.DataServices.impl.Helpers;
using Payment.API.DataServices.interfaces.Helpers;
using Payment.Hubs;
using Payment.Protocol;
using Payment.Protocol.Base;
using Payment.Protocol.Dto;
using Payment.Protocol.Dtos;
using Payment.Shared.Dto;
using Payment.Shared.Requests;
using Payment.Shared.Responses;
using System.Globalization;
using System.Text;

namespace AtmService.Services
{
    public sealed class WithdrawalsService : IWithdrawalService
    {
        private readonly IGtClient _gt;
        private readonly IStenGenerator _stan;
        private readonly IHubContext<AtmHub> _hub;
        private readonly ILogger<WithdrawalsService> _log;

        public WithdrawalsService(
            IGtClient gt,
            IStenGenerator stan,
            IHubContext<AtmHub> hub,
            ILogger<WithdrawalsService> log)
        {
            _gt = gt;
            _stan = stan;
            _hub = hub;
            _log = log;
        }

        public async Task<ReserveWithdrawalResponse> ReserveAsync(ReserveWithdrawalRequest req, CancellationToken ct)
        {
            // 1) Generated fields
            var correlationId = Guid.NewGuid().ToString("D");
            var stan = _stan.Next();

            // 2) Toy PIN block per assignment
            var pinBlock = ToyPinBlock.Compute(req.Pan, req.Pin, correlationId);

            using var scope = _log.BeginScope(new Dictionary<string, object?>
            {
                ["atmId"] = req.AtmId,
                ["correlationId"] = correlationId,
                ["stan"] = stan
            });

            // 3) Publish request event
            await PublishAsync(req.AtmId, correlationId, new AtmEventDto(
                EventType: "ReserveRequest",
                AtmId: req.AtmId,
                CorrelationId: correlationId,
                Stan: stan,
                Rc: "",
                AuthCode: null,
                Message: null,
                TsUtc: DateTimeOffset.UtcNow
            ));


            // request 
            var request = new A70RequestDto
            {
                AtmId = req.AtmId,
                CorrelationId = correlationId,
                Stan = stan,
                Pan = req.Pan,
                ExpiryYYMM = req.ExpiryYYMM,
                PinBlock = pinBlock,
                AmountMinor = req.AmountMinor,
                Currency = req.Currency
            };

            ParsedFrame a71;
            try
            {
                // send message and wait for response 
                a71 = await _gt.SendAndWaitWithRetryAsync(request, ct);
            }
            catch (TimeoutException)
            {
                // Expected failure mode: no GT response after retries
                // Keep your mapping (you used 91). Message clarifies it's GT no-response.
                const string rc = "91";
                const string msg = "NO_RESPONSE_FROM_GT";

                var timeoutResult = new ReserveWithdrawalResponse
                {
                    CorrelationId = correlationId,
                    Stan = stan,
                    Rc = rc,
                    AuthCode = null,
                    Message = msg
                };

                await PublishAsync(req.AtmId, correlationId, new AtmEventDto(
                    EventType: "ReserveResult",
                    AtmId: req.AtmId,
                    CorrelationId: correlationId,
                    Stan: stan,
                    Rc: rc,
                    AuthCode: null,
                    Message: msg,
                    TsUtc: DateTimeOffset.UtcNow
                ));

                return timeoutResult;
            }

            // Anything else (IO, socket, unexpected) bubbles to global exception handler

            // Validate message type
            if (a71.MsgType != MessageTypes.A71)
                throw new InvalidOperationException($"Expected A71 but got type=0x{a71.MsgType:X2}");

            // Parse response TLVs
            var rcOk = a71.GetAsciiOrNull(Tags.Rc) ?? "96";
            var authCode = a71.GetAsciiOrNull(Tags.AuthCode);
            var message = a71.GetAsciiOrNull(Tags.Message);

            var result = new ReserveWithdrawalResponse {
                CorrelationId = correlationId,
                Stan = stan,
                Rc = rcOk,
                AuthCode = authCode,
                Message = message
            };

            await PublishAsync(req.AtmId, correlationId, new AtmEventDto(
                EventType: "ReserveResult",
                AtmId: req.AtmId,
                CorrelationId: correlationId,
                Stan: stan,
                Rc: rcOk,
                AuthCode: authCode,
                Message: message,
                TsUtc: DateTimeOffset.UtcNow
            ));

            return result;
        }


        public async Task<CompleteWithdrawalResponse> CompleteAsync(CompleteWithdrawalRequest req, CancellationToken ct)
        {
            // 1) Generate fields for THIS completion message
            var stan = _stan.Next();

            using var scope = _log.BeginScope(new Dictionary<string, object?>
            {
                ["atmId"] = req.AtmId,
                ["correlationId"] = req.CorrelationId,
                ["stan"] = stan,
                ["originalStan"] = req.OriginalStan
            });

            await PublishAsync(req.AtmId, req.CorrelationId, new AtmEventDto(
                EventType: "CompleteRequest",
                AtmId: req.AtmId,
                CorrelationId: req.CorrelationId,
                Stan: stan,
                Rc: "",
                AuthCode: null,
                Message: null,
                TsUtc: DateTimeOffset.UtcNow
            ));


            var request =  new A72RequestDto
            {
                AtmId = req.AtmId,
                CorrelationId = req.CorrelationId,
                Stan = stan,
                OriginalStan = req.OriginalStan,
                DispenseResult = req.DispenseResult,
                DispenseAmountMinor = req.DispensedAmountMinor
            };

      
            ParsedFrame a73;
            try
            {
                a73 = await _gt.SendAndWaitWithRetryAsync(request, ct);
            }
            catch (TimeoutException)
            {
                const string rc = "91";
                const string msg = "NO_RESPONSE_FROM_GT (OUTCOME_UNKNOWN)";

                var timeoutResult = new CompleteWithdrawalResponse
                {
                    CorrelationId = req.CorrelationId,
                    Rc = rc,
                    Stan = stan,
                    Message = msg
                };

                await PublishAsync(req.AtmId, req.CorrelationId, new AtmEventDto(
                    EventType: "CompleteResult",
                    AtmId: req.AtmId,
                    CorrelationId: req.CorrelationId,
                    Stan: stan,
                    Rc: rc,
                    AuthCode: null,
                    Message: msg,
                    TsUtc: DateTimeOffset.UtcNow
                ));

                return timeoutResult;
            }

            if (a73.MsgType != MessageTypes.A73)
                throw new InvalidOperationException($"Expected A73 but got type=0x{a73.MsgType:X2}");

            var rcOk = a73.GetAsciiOrNull(Tags.Rc) ?? "96";
            var message = a73.GetAsciiOrNull(Tags.Message);

            var result = new CompleteWithdrawalResponse
            {
                CorrelationId = req.CorrelationId,
                Stan = stan,
                Rc = rcOk,
                Message = message
            };

            await PublishAsync(req.AtmId, req.CorrelationId, new AtmEventDto(
                EventType: "CompleteResult",
                AtmId: req.AtmId,
                CorrelationId: req.CorrelationId,
                Stan: stan,
                Rc: rcOk,
                AuthCode: null,
                Message: message,
                TsUtc: DateTimeOffset.UtcNow
            ));

            return result;
        }
        private Task PublishAsync(string atmId, string correlationId, AtmEventDto evt)
        {
            // GUI can subscribe to either ATM-wide stream or per-transaction stream
            return Task.WhenAll(
                _hub.Clients.Group($"atm:{atmId}").SendAsync("atmEvent", evt),
                _hub.Clients.Group($"txn:{correlationId}").SendAsync("atmEvent", evt)
            );
        }
    }
}
