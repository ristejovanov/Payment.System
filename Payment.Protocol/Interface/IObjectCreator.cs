namespace Payment.Protocol.Interface
{
    /// <summary>
    /// Defines methods for serializing and deserializing objects to and from byte arrays.
    /// </summary>
    public interface IObjectCreator
    {
        /// <summary>
        /// Converts an object to a byte array representation.
        /// </summary>
        /// <param name="obj">The object to serialize.</param>
        /// <returns>A byte array representation of the object.</returns>
        byte[] ToBytes(object obj, bool skipEmptyStrings = true, bool skipDefaultNumbers = true);

    }
}
