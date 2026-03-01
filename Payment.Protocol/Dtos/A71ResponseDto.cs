using Payment.Protocol.Impl.Base;

namespace Payment.Protocol.Dtos
{
    public class A71ResponseDto : ResponseDto
    {
        public override byte MsgType => MessageTypes.A71;

        [TlvTag(Tags.AuthCode)]
        public string? AuthCode { get; set; }

        [TlvTag(Tags.DispensedAmountMinor)]
        public string? DispensedAmountMinor { get; set; }
    }
}
