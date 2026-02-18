using Payment.Protocol.Base;
using Payment.Protocol.Dtos;
using Payment.Protocol.Impl.Base;

namespace Payment.Protocol.Dto
{
    public sealed class A70RequestDto : RequestDto
    {
        public override byte MsgType => MessageTypes.A70;

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
