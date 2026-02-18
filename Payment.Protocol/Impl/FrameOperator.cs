using Payment.Protocol.Interface;
using System.Buffers;
using System.Buffers.Binary;
using System.Globalization;
using System.Text;

namespace Payment.Protocol.Impl
{
    public class FrameOperator : IFrameOperator
    {
        public byte[] FrameToBinary(Frame frame)
        {
            // payload = msgType(1) + version(1) + TLVs
            int tlvBytes = 0;
            foreach (var t in frame.Tlvs)
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

            span[0] = frame.MsgType;
            span[1] = frame.Version;
            span = span.Slice(2);

            foreach (var t in frame.Tlvs)
            {
                span[0] = t.Tag;
                span[1] = (byte)t.Value.Length;
                span = span.Slice(2);

                t.Value.Span.CopyTo(span.Slice(0, t.Value.Length));
                span = span.Slice(t.Value.Length);
            }

            return buf;
        }

        public bool BinaryToFrame(ref ReadOnlySequence<byte> buffer, out Frame? frame)
        {
            frame = null;
            var sr = new SequenceReader<byte>(buffer);

            if (!sr.TryReadBigEndian(out short payloadLen))
                return false;

            if (sr.Remaining < payloadLen)
                return false;

            // Take payload slice (zero-copy)
            var payload = buffer.Slice(sr.Position, payloadLen);
            sr.Advance(payloadLen);

            // Consume from original buffer: LEN + PAYLOAD
            buffer = buffer.Slice(sr.Position);

            frame = ParsePayload(payload);
            return true;
        }

        private static Frame ParsePayload(in ReadOnlySequence<byte> payload)
        {
            var r = new SequenceReader<byte>(payload);

            if (!r.TryRead(out byte msgType))
                throw new InvalidOperationException("Missing MSG_TYPE.");

            if (!r.TryRead(out byte version))
                throw new InvalidOperationException("Missing VERSION.");

            var tlvs = new List<Tlv>(8);

            while (r.Remaining > 0)
            {
                if (!r.TryRead(out byte tag)) throw new InvalidOperationException("Bad TLV tag.");
                if (!r.TryRead(out byte len)) throw new InvalidOperationException("Bad TLV len.");
                if (r.Remaining < len) throw new InvalidOperationException("Bad TLV value length.");

                var valueSeq = payload.Slice(r.Position, len);
                r.Advance(len);

                ReadOnlyMemory<byte> valueMem = valueSeq.IsSingleSegment ? valueSeq.First : valueSeq.ToArray();
                tlvs.Add(new Tlv(tag, valueMem));
            }

            return new Frame { MsgType = msgType, Version = version, Tlvs = tlvs };
        }


        private Tlv Ascii(byte tag, string s) => new(tag, Encoding.ASCII.GetBytes(s));

        private Tlv Digits(byte tag, long value)
            => new(tag, Encoding.ASCII.GetBytes(value.ToString(CultureInfo.InvariantCulture)));

        private Tlv Digits(byte tag, int value)
            => new(tag, Encoding.ASCII.GetBytes(value.ToString(CultureInfo.InvariantCulture)));

        public string ToHex(ReadOnlySpan<byte> bytes)
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
