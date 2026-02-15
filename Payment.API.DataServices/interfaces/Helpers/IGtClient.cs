using Payment.Protocol;

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
        Task<ParsedFrame> SendAndWaitWithRetryAsync(byte msgType, IReadOnlyList<Tlv> tlvs, string correlationId, CancellationToken ct)
    }
}