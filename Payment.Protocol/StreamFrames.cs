using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Text;

namespace Payment.Protocol
{
    public static class StreamFrames
    {
        public static async Task WriteFrameAsync(Stream stream, TlvMessage msg, CancellationToken ct)
        {
            var payload = Codec.EncodePayload(msg);
            var frame = Codec.Frame(payload);
            await stream.WriteAsync(frame, ct);
            await stream.FlushAsync(ct);
        }

        public static async Task<TlvMessage> ReadFrameAsync(Stream stream, CancellationToken ct)
        {
            var lenBuf = await ReadExactAsync(stream, 2, ct);
            var len = BinaryPrimitives.ReadUInt16BigEndian(lenBuf);

            var payload = await ReadExactAsync(stream, len, ct);
            return Codec.DecodePayload(payload);
        }

        private static async Task<byte[]> ReadExactAsync(Stream stream, int bytes, CancellationToken ct)
        {
            var buf = new byte[bytes];
            var read = 0;
            while (read < bytes)
            {
                var n = await stream.ReadAsync(buf.AsMemory(read, bytes - read), ct);
                if (n == 0) throw new IOException("Connection closed");
                read += n;
            }
            return buf;
        }
    }
}
