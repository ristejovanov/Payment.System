namespace Payment.Shared.Enums
{
    public enum CompletionStatus
    {
        Completed = 1,        // dispensed == reserved
        Partial = 2,          // 0 < dispensed < reserved
        Failed = 3,           // dispensed == 0 or dispenseResult FAILED
        Rejected = 4          // completion invalid (e.g. dispensed > reserved)
    }
}
