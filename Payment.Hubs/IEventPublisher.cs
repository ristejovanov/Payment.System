
/// <summary>
/// Defines a contract for publishing payment-related events.
/// </summary>
namespace Payment.Hubs
{
    public interface IEventPublisher
    {
        /// <summary>
        /// Publishes an event asynchronously with the specified parameters.
        /// </summary>
        /// <param name="eventType">The type of event to publish.</param>
        /// <param name="atmId">The ATM identifier.</param>
        /// <param name="stan">The system trace audit number.</param>
        /// <param name="correlationId">The correlation identifier for tracking the event.</param>
        /// <param name="rc">The response code.</param>
        /// <param name="authCode">Optional authorization code.</param>
        /// <param name="message">Optional message associated with the event.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task PublishAsync(string eventType, string atmId, long stan, string correlationId, string rc, string authCode = null, string message = null);
    }
}