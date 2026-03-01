namespace Payment.Shared.Dto
{
    /// <summary>
    /// Event data for ATM transaction events published via SignalR
    /// </summary>
    public record AtmEventDto(
        string EventType,
        string AtmId,
        string CorrelationId,
        long Stan,
        string Rc,
        string? AuthCode,
        string? Message,
        DateTimeOffset TsUtc
    );
}