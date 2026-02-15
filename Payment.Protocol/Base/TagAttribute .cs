namespace Payment.Protocol.Base
{

    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
    public sealed class TlvTagAttribute : Attribute
    {
        public byte Tag { get; }
        public TlvTagAttribute(byte tag) => Tag = tag;
    }
}
