using Payment.Shared.Enums;

namespace Payment.Shared.Dto
{
    public sealed record CompleteReservationRequest(
         string AtmId,
         long Stan,
         string CorrelationId,
         int AmountMinor,
         string Currency,
         string Fingerprint,
         string Rc,
         string? AuthCode,
         string? Message,
         byte[] A71Bytes,
         DateTimeOffset CompletedUtc);
}
