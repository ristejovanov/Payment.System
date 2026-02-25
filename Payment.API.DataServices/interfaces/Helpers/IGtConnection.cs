using Payment.Protocol;

namespace Payment.API.DataServices.interfaces.Helpers
{
    public interface IGtConnection : IAsyncDisposable
    {
        event Action<Frame> FrameReceived;

        Task EnsureConnectedAsync(CancellationToken ct);
        Task ForceReconnectAsync(CancellationToken ct);

        Task SendAsync(byte[] frameBytes, CancellationToken ct);
    }
}
