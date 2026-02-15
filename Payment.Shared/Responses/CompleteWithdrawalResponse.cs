namespace Payment.Shared.Responses
{
    public class CompleteWithdrawalResponse
    {
        public required string CorrelationId { get; set; }
        public required long Stan { get; set; }
        public required string Rc { get; set; }
        public string? Message { get; set; }
    }
}
