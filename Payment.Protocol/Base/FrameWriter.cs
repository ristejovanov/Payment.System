using System.Buffers.Binary;
using System.Globalization;
using System.Text;

namespace Payment.Protocol.Base
{
    public static class FrameWriter
    {
        public static byte[] BuildFrame(byte msgType, IReadOnlyList<Tlv> tlvs)
        {
            // payload = msgType(1) + version(1) + TLVs
            int tlvBytes = 0;
            foreach (var t in tlvs)
            {
                if (t.Value.Length > 255) throw new InvalidOperationException("TLV value too long for 1-byte LEN.");
                tlvBytes += 1 + 1 + t.Value.Length;
            }

            int payloadLen = 1 + 1 + tlvBytes;
            if (payloadLen > ushort.MaxValue) throw new InvalidOperationException("Payload too large.");

            var buf = new byte[2 + payloadLen];
            var span = buf.AsSpan();

            BinaryPrimitives.WriteUInt16BigEndian(span, (ushort)payloadLen);
            span = span.Slice(2);

            span[0] = msgType;
            span[1] = MessageTypes.Version;
            span = span.Slice(2);

            foreach (var t in tlvs)
            {
                span[0] = t.Tag;
                span[1] = (byte)t.Value.Length;
                span = span.Slice(2);

                t.Value.Span.CopyTo(span.Slice(0, t.Value.Length));
                span = span.Slice(t.Value.Length);
            }

            return buf;
        }

        public static Tlv Ascii(byte tag, string s) => new(tag, Encoding.ASCII.GetBytes(s));

        public static Tlv Digits(byte tag, long value)
            => new(tag, Encoding.ASCII.GetBytes(value.ToString(CultureInfo.InvariantCulture)));

        public static Tlv Digits(byte tag, int value)
            => new(tag, Encoding.ASCII.GetBytes(value.ToString(CultureInfo.InvariantCulture)));

        public static string ToHex(ReadOnlySpan<byte> bytes)
        {
            var sb = new StringBuilder(bytes.Length * 3);
            for (int i = 0; i < bytes.Length; i++)
            {
                if (i > 0) sb.Append(' ');
                sb.Append(bytes[i].ToString("X2"));
            }
            return sb.ToString();
        }
    }
}
