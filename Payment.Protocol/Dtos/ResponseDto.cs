using Payment.Protocol.Impl.Base;
namespace Payment.Protocol.Dtos 
{
    public abstract class ResponseDto :MessageBase
    {
        [TlvTag(Tags.Rc)]
        public string Rc { get; set; }

        [TlvTag(Tags.CorrelationId)]
        public string CorrelationId { get; set; }

        [TlvTag(Tags.Message)]
        public string? Message { get; set; }
    }
}
