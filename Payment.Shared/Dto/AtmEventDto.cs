
namespace Payment.Shared.Dto
{
    public sealed record AtmEventDto(
      string EventType,          // "ReserveRequest", "ReserveResult", "CompleteRequest", "CompleteResult"
      string AtmId,
      string CorrelationId,
      long Stan,
      string Rc,
      string? AuthCode,
      string? Message,
      DateTimeOffset TsUtc
  );
}
