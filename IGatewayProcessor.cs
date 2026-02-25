using Payment.Protocol;

namespace Payment.GT.Classes.Interface
{
    public interface IGatewayProcessor
    {
        Task<byte[]> HandleAsync(Frame req, CancellationToken ct);
    }
}