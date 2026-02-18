using System;
using System.Buffers;
using System.Collections.Generic;
using System.Text;

namespace Payment.Protocol
{
    public  class Frame
    {
        public required byte MsgType { get; set; }
        public required byte Version { get; set; } 
        public required IReadOnlyList<Tlv> Tlvs { get; set; }

        public string? GetAsciiOrNull(byte tag)
        {
            var tlv = Tlvs.FirstOrDefault(x => x.Tag == tag);
            return tlv.Value.IsEmpty ? null : Encoding.ASCII.GetString(tlv.Value.Span);
        }
    }
}
