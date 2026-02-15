using System;
using System.Buffers;
using System.Collections.Generic;
using System.Text;

namespace Payment.Protocol
{

    public sealed class ParsedFrame
    {
        public required byte MsgType { get; init; }
        public required byte Version { get; init; } // 0x10
        public required IReadOnlyList<Tlv> Tlvs { get; init; }

        public string? GetAsciiOrNull(byte tag)
        {
            var tlv = Tlvs.FirstOrDefault(x => x.Tag == tag);
            return tlv.Value.IsEmpty ? null : Encoding.ASCII.GetString(tlv.Value.Span);
        }
    }

    public static class FrameParser
    {
        // Frame: LEN(2 BE) + PAYLOAD where PAYLOAD = MsgType(1) + Version(1) + TLVs
        public static bool TryReadFrame(ref ReadOnlySequence<byte> buffer, out ParsedFrame? frame)
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

        private static ParsedFrame ParsePayload(in ReadOnlySequence<byte> payload)
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

            return new ParsedFrame { MsgType = msgType, Version = version, Tlvs = tlvs };
        }
    }
}
