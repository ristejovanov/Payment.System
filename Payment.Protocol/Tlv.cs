using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Payment.Protocol
{
    public readonly record struct Tlv(byte Tag, ReadOnlyMemory<byte> Value)
    {
        public byte Len => checked((byte)Value.Length);
        public string Ascii() => Encoding.ASCII.GetString(Value.Span);


        public static Tlv Ascii(byte tag, string value)
            => new(tag, Encoding.ASCII.GetBytes(value));

        public static Tlv Digits(byte tag, long value)
            => new(tag, Encoding.ASCII.GetBytes(value.ToString(CultureInfo.InvariantCulture)));

        public static Tlv Digits(byte tag, int value)
            => new(tag, Encoding.ASCII.GetBytes(value.ToString(CultureInfo.InvariantCulture)));

    }
}
