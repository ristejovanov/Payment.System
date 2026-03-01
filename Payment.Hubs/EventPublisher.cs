using Payment.API.DataServices.interfaces.Helpers;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace Payment.Hubs
{
    public class EventPublisher : IEventPublisher
    {
        private readonly IHubContext<AtmHub> _hub;
        private readonly ILogger<EventPublisher> _logger;

        public EventPublisher(IHubContext<AtmHub> hub, ILogger<EventPublisher> logger)
        {
            _hub = hub;
            _logger = logger;
        }

        public Task PublishAsync(
            string eventType,
            string atmId,
            long stan,
            string correlationId,
            string rc,
            string? authCode = null,
            string? message = null)
        {
            // Create the event DTO inside the publisher
            var evt = new AtmEventDto(
                EventType: eventType,
                AtmId: atmId,
                CorrelationId: correlationId,
                Stan: stan,
                Rc: rc,
                AuthCode: authCode,
                Message: message,
                TsUtc: DateTimeOffset.UtcNow
            );

            // Fire-and-forget: don't await the publishing
            _ = PublishInternalAsync(evt, atmId, correlationId, eventType, stan);

            // Return completed task immediately
            return Task.CompletedTask;
        }

        private async Task PublishInternalAsync(
            AtmEventDto evt,
            string atmId,
            string correlationId,
            string eventType,
            long stan)
        {
            try
            {
                // Publish to SignalR groups (ATM-wide and per-transaction)
                await Task.WhenAll(
                    _hub.Clients.Group($"atm:{atmId}").SendAsync("atmEvent", evt),
                    _hub.Clients.Group($"txn:{correlationId}").SendAsync("atmEvent", evt)
                );

                _logger.LogDebug(
                    "Event published: {EventType} for ATM: {AtmId}, STAN: {Stan}, CorrelationId: {CorrelationId}",
                    eventType, atmId, stan, correlationId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Failed to publish event: {EventType} for ATM: {AtmId}, CorrelationId: {CorrelationId}",
                    eventType, atmId, correlationId);
                // Don't rethrow - event publishing failures shouldn't break the main flow
            }
        }
    }
}