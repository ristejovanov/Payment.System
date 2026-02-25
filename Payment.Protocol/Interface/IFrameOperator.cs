using System.Buffers;

namespace Payment.Protocol.Interface
{
    /// <summary>
    /// Defines operations for converting between Frame objects and their binary representations.
    /// </summary>
    public interface IFrameOperator
    {
        /// <summary>
        /// Converts a Frame object into its binary representation.
        /// </summary>
        /// <param name="frame">The Frame object to convert.</param>
        /// <returns>A byte array containing the binary representation of the frame.</returns>
        byte[] FrameToBinary(Frame frame);

        /// <summary>
        /// Attempts to parse a Frame object from a binary buffer.
        /// </summary>
        /// <param name="buffer">The buffer containing the binary data to parse. The buffer is advanced if parsing succeeds.</param>
        /// <param name="frame">When this method returns, contains the parsed Frame if successful; otherwise, null.</param>
        /// <returns>true if a frame was successfully parsed; otherwise, false.</returns>
        bool BinaryToFrame(ref ReadOnlySequence<byte> buffer, out Frame? frame);

        /// <summary>
        /// Converts a byte span to its hexadecimal string representation.
        /// </summary>
        /// <param name="bytes">The byte span to convert.</param>
        /// <returns>A string containing the hexadecimal representation of the bytes.</returns>
        string ToHex(ReadOnlySpan<byte> bytes);
    }
}
