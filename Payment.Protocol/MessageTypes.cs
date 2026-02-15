using System;
using System.Collections.Generic;
using System.Text;

namespace Payment.Protocol
{
    public class MessageTypes
    {
        public const byte A70 = 0x70;
        public const byte A71 = 0x71;
        public const byte A72 = 0x72;
        public const byte A73 = 0x73;

        public const byte Version = 0x10;

        // Heartbeat
        public const byte Ping = 0x7F; // ATM -> GT
        public const byte Pong = 0x7E; // GT  -> ATM
    }
}
