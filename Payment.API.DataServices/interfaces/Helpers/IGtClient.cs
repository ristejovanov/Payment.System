using Payment.Protocol;
using Payment.Protocol.Dtos;

namespace Payment.API.DataServices.impl.Helpers
{
    /// <summary>
    /// Interface for communicating with GT Gateway over TCP
    /// </summary>
    public interface IGtClient : IAsyncDisposable
    {
        /// <summary>
        /// Send message to GT with retry logic
        /// </summary>
        Task<Frame> SendAndWaitWithRetryAsync(RequestDto request, CancellationToken ct);
    }
}