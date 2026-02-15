namespace Payment.Shared.Dto
{
    public class IssuerDecision
    {
        public required string Rc { get; set; }
        public string? AuthCode { get; set; }
        public string Message { get; set; }
    }
}
