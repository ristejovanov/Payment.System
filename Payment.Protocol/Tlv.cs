using System;
using System.Collections.Generic;
using System.Text;

namespace Payment.Protocol
{
    public readonly record struct Tlv(byte Tag, ReadOnlyMemory<byte> Value)
    {
        public byte Len => checked((byte)Value.Length);

        public static Tlv Ascii(byte tag, string value)
            => new(tag, Encoding.ASCII.GetBytes(value));

        public string Ascii() => Encoding.ASCII.GetString(Value.Span);
    }
}
