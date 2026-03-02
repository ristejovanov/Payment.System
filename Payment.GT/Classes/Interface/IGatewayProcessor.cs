using Payment.Protocol;

namespace Payment.GT.Classes.Interface
{
    /// <summary>
    /// Processes incoming gateway frames (A70 reserve, A72 complete, heartbeat)
    /// and returns protocol-compliant binary responses.
    /// </summary>
    public interface IGatewayProcessor
    {
        /// <summary>
        /// Handles an incoming frame and returns the response bytes.
        /// </summary>
        /// <param name="req">The incoming frame to process</param>
        /// <param name="ct">Cancellation token</param>
        /// <returns>Binary response frame</returns>
        Task<byte[]> HandleAsync(Frame req, CancellationToken ct);
    }
}