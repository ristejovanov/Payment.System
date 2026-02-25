using Payment.Protocol.Impl.Base;

namespace Payment.Protocol.Dtos
{
    public abstract class RequestDto : MessageBase
    {
        [TlvTag(Tags.AtmId)]
        public string AtmId { get; set; }

        [TlvTag(Tags.Stan)]
        public long Stan { get; set; }

        [TlvTag(Tags.LocalDateTime)]
        public DateTime LocalDateTime { get; set; } = DateTime.UtcNow;

        [TlvTag(Tags.CorrelationId)]
        public string CorrelationId { get; set; }

        [TlvTag(Tags.IsRepeat)]
        public bool IsRepeat { get; set; } = false;
    }
}
