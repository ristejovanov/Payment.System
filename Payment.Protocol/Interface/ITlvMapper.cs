using Payment.Protocol.Impl.Base;

namespace Payment.Protocol.Interface
{
    /// <summary>
    /// Interface for TLV serialization/deserialization.
    /// </summary>
    public interface ITlvMapper
    {
        /// <summary>
        /// Convert DTO to TLV list
        /// </summary>
        IReadOnlyList<Tlv> ToTlvs(object obj, bool skipEmptyStrings, bool skipDefaultNumbers);

        /// <summary>
        /// Convert TLV list to DTO
        /// </summary>
        T FromTlvs<T>(IReadOnlyList<Tlv> tlvs) where T : new();
    }
}
