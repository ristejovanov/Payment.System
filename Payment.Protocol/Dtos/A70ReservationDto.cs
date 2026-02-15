
using Payment.Protocol;
using Payment.Protocol.Base;

namespace Payment.Shared.Dto
{
    public sealed class A70ReservationDto : MessageBase
    {
        public override byte MsgType => MessageTypes.A70;

        [TlvTag(Tags.AtmId)]
        public string AtmId { get; set; }

        [TlvTag(Tags.Stan)]
        public long Stan { get; set; }

        [TlvTag(Tags.LocalDateTime)]
        public DateTime LocalDateTime { get; set; }

        [TlvTag(Tags.CorrelationId)]
        public string? CorrelationId { get; set; }

        [TlvTag(Tags.IsRepeat)]
        public bool IsRepeat { get; set; }

        [TlvTag(Tags.Pan)]
        public string? Pan { get; set; }

        [TlvTag(Tags.ExpiryYYMM)]
        public string? ExpiryYYMM { get; set; }

        [TlvTag(Tags.PinBlock) ]
        public string? PinBlock { get; set; }

        [TlvTag(Tags.AmountMinor)]
        public int AmountMinor { get; set; }

        [TlvTag(Tags.Currency)]
        public string? Currency { get; set; }
    }
}
