using System;
using System.Collections.Generic;
using System.Text;

namespace Payment.Protocol.Base
{
    public abstract class MessageBase
    {
        public abstract byte MsgType { get; }
        protected virtual bool SkipEmptyStrings => true;
        protected virtual bool SkipDefaultNumbers => false; // set true if you want "0" skipped

        public IReadOnlyList<Tlv> ToTlvs()
            => TlvReflectionMapper.ToTlvs(this, SkipEmptyStrings, SkipDefaultNumbers);

        public virtual byte[] BuildFrame()
            => FrameWriter.BuildFrame(MsgType, ToTlvs());

        public virtual string BuildFrameHex()
            => FrameWriter.ToHex(BuildFrame());
    }
}
