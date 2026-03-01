using Payment.Protocol.Impl.Base;
using Payment.Shared.Enums;

namespace Payment.Protocol.Dtos
{
    public class A72RequestDto : RequestDto
    {
        public override byte MsgType => MessageTypes.A72;
  
        [TlvTag(Tags.OriginalStan)]
        public long OriginalStan { get; set; }

        [TlvTag(Tags.DispensedAmountMinor)]
        public int DispenseAmountMinor { get; set; }

        [TlvTag(Tags.DispenseResult)]
        public DispenseResultType DispenseResult { get; set; }
    }
}
