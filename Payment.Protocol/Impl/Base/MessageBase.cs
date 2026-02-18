using System;
using System.Collections.Generic;
using System.Text;

namespace Payment.Protocol.Impl.Base
{
    public abstract class MessageBase
    {
        public abstract byte MsgType { get; }
        public abstract byte Version { get; }     
    }
}
