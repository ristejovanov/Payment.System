using Payment.Protocol.Impl.Base;
using Payment.Shared.Enums;

namespace Payment.Protocol.Dtos
{
    public class A73ResponseDto : ResponseDto
    {
        public override byte MsgType => MessageTypes.A73;

        [TlvTag(Tags.CompletionStatus)]
        public CompletionStatus CompletionStatus { get; set; }      
    }
}
