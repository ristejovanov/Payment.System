using Payment.Shared.Enums;
namespace Payment.Shared.Dto
{


    public sealed record ReservationRecord(
        string AtmId,
        long Stan,
        string CorrelationId,
        string Fingerprint,
        ReservationStatus Status,
        int ReservedAmountMinor,
        string Currency,
        string Rc,
        string? AuthCode,
        string? Message,
        DateTimeOffset CreatedUtc,
        DateTimeOffset? CompletedUtc
    );


    public sealed record ReservationEntry(
        ReservationRecord Record,
        byte[]? A71Bytes
    );
}
