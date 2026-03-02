namespace Payment.Shared.Dto
{
    public sealed class GtClientOptions
    {
        public string Host { get; init; } = "localhost";
        public int Port { get; init; } = 5000;

        public int TimeoutMs { get; init; } = 2000;
        public int MaxRetries { get; init; } = 2;

        // Heartbeat enabled
        public int HeartbeatSeconds { get; init; } = 15;
        public int HeartbeatTimeoutMs { get; init; } = 2000;
    }
}
