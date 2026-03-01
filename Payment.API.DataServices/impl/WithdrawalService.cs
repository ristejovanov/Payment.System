using ATM.DataServices.interfaces;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Payment.API.DataServices.impl.Helpers;
using Payment.API.DataServices.interfaces.Helpers;
using Payment.Hubs;
using Payment.Protocol;
using Payment.Protocol.Dto;
using Payment.Protocol.Dtos;
using Payment.Protocol.Interface;
using Payment.Shared.Dto;
using Payment.Shared.Requests;
using Payment.Shared.Responses;

namespace AtmService.Services
{
    public sealed class WithdrawalsService : IWithdrawalService
    {
        private readonly IGtClient _gt;
        private readonly IStenGenerator _stan;
        private readonly IEventPublisher _eventPublisher; // Changed from IHubContext
        private readonly ILogger<WithdrawalsService> _log;
        private readonly ITlvMapper _mapper;


        public WithdrawalsService(
            IGtClient gt,
            IStenGenerator stan,
            IHubContext<AtmHub> hub,
            ILogger<WithdrawalsService> log, 
            ITlvMapper mapper,
            IEventPublisher eventPublisher)
        {
            _gt = gt;
            _stan = stan;
            _log = log;
            _mapper = mapper;
            _eventPublisher = eventPublisher;
        }

        public async Task<ReserveWithdrawalResponse> ReserveAsync(ReserveWithdrawalRequest req, CancellationToken ct)
        {
            //  Generated fields
            var correlationId = Guid.NewGuid().ToString("D");
            var stan = _stan.Next();

            // Toy PIN block per assignment
            var pinBlock = ToyPinBlock.Compute(req.Pan, req.Pin, correlationId);

            // Log context for all events related to this request
            using var scope = _log.BeginScope(new Dictionary<string, object?>
            {
                ["Request"] = "ReserveWithdrawal",
                ["atmId"] = req.AtmId,
                ["stan"] = stan,
                ["correlationId"] = correlationId,
            });

            // publisuh event to signalR
            await _eventPublisher.PublishAsync("ReserveRequest", req.AtmId, stan, correlationId, "");

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

            Frame a71;
            try
            {
                // send message and wait for response 
                a71 = await _gt.SendAndWaitWithRetryAsync(request, ct);
            }
            catch (TimeoutException)
            {
                const string rc = "91";
                const string msg = "No response from GT";

                var timeoutResult = new ReserveWithdrawalResponse
                {
                    CorrelationId = correlationId,
                    Stan = stan,
                    Rc = rc,
                    AuthCode = null,
                    Message = msg
                };

                await _eventPublisher.PublishAsync("ReserveRequest", req.AtmId, stan, correlationId, rc, "", msg);
                return timeoutResult;
            }


            // try  to parse response, if parsing fails log error and return generic failure response
            A71ResponseDto respons;
            try 
            {             
                respons = _mapper.FromTlvs<A71ResponseDto>(a71.Tlvs);
            }catch (Exception ex)
            {
                _log.LogError(ex, "Failed to parse GT response");
                throw new InvalidOperationException("Failed to parse GT response") ;
            }

            var result = new ReserveWithdrawalResponse {
                CorrelationId = correlationId,
                Stan = stan,
                Rc = respons.Rc,
                AuthCode = respons.AuthCode,
                Message = respons.Message
            };

            await _eventPublisher.PublishAsync("ReserveRequest", req.AtmId, stan, correlationId, respons.Rc, respons.AuthCode, respons.Message);
            return result;
        }


        public async Task<CompleteWithdrawalResponse> CompleteAsync(CompleteWithdrawalRequest req, CancellationToken ct)
        {
            // Generate fields for THIS completion message
            var stan = _stan.Next();

            // Log context for all events related to this request
            using var scope = _log.BeginScope(new Dictionary<string, object?>
            {
                ["Request"] = "CompleteResult",
                ["atmId"] = req.AtmId,
                ["stan"] = stan,
                ["correlationId"] = req.CorrelationId,
            });

            // event notification
            await _eventPublisher.PublishAsync("CompleteResult", req.AtmId, stan, req.CorrelationId, "");

            // request
            var request =  new A72RequestDto
            {
                AtmId = req.AtmId,
                CorrelationId = req.CorrelationId,
                Stan = stan,
                OriginalStan = req.OriginalStan,
                DispenseResult = req.DispenseResult,
                DispenseAmountMinor = req.DispensedAmountMinor
            };

      
            Frame a73;
            try
            {
                // send message and wait for response
                a73 = await _gt.SendAndWaitWithRetryAsync(request, ct);
            }
            catch (TimeoutException)
            {
                const string rc = "91";
                const string msg = "No response from GT (Outcome uncnown)";

                var timeoutResult = new CompleteWithdrawalResponse
                {
                    CorrelationId = req.CorrelationId,
                    Rc = rc,
                    Stan = stan,
                    Message = msg
                };

                // event notification
                await _eventPublisher.PublishAsync("CompleteResult", req.AtmId, stan, req.CorrelationId, rc, "", msg);
                return timeoutResult;
            }


            // try  to parse response, if parsing fails log error and return generic failure response
            A73ResponseDto respons;
            try
            {
                respons = _mapper.FromTlvs<A73ResponseDto>(a73.Tlvs);
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Failed to parse GT response");
                throw new InvalidOperationException("Failed to parse GT response");
            }

            // event notification
            await _eventPublisher.PublishAsync("CompleteResult", req.AtmId, stan, req.CorrelationId, respons.Rc, "", respons.Message);
            return new CompleteWithdrawalResponse
            {
                CorrelationId = req.CorrelationId,
                Stan = stan,
                Rc = respons.Rc,
                Message = respons.Message
            };
        }
    }
}
