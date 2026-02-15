namespace Payment.Shared.Dto
{
    public sealed class GtClientOptions
    {
        public string Host { get; init; } = "localhost";
        public int Port { get; init; } = 5000;

        public int TimeoutMs { get; init; } = 2000;
        public int MaxRetries { get; init; } = 2;

        public int ConnectTimeoutMs { get; init; } = 2000;
        public int ReconnectMinDelayMs { get; init; } = 200;
        public int ReconnectMaxDelayMs { get; init; } = 5000;

        public int MaxInFlight { get; init; } = 500;
        public int LateResponseRetentionSeconds { get; init; } = 30;

        // Heartbeat enabled
        public int HeartbeatSeconds { get; init; } = 15;
        public int HeartbeatTimeoutMs { get; init; } = 2000;
    }
}
