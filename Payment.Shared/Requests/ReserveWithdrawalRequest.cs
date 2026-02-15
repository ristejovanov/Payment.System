using System.ComponentModel.DataAnnotations;

namespace Payment.Shared.Requests
{
    public class ReserveWithdrawalRequest : BaseRequestModel
    {
        public required string AtmId { get; set; }
        public required string Pan { get; set; }
        public required string ExpiryYYMM { get; set; }
        public required string Pin { get; set; }
        public int AmountMinor { get; set; }
        public required string Currency { get; set; }

        protected override void ValidateModel(ValidationContext validationContext)
        {
            ValidateRequired(AtmId, nameof(AtmId));

            ValidateRequired(Pan, nameof(Pan));
            ValidateStringLength(Pan, nameof(Pan), 13, 19);
            ValidateDigitsOnly(Pan, nameof(Pan));

            ValidateRequired(Pin, nameof(Pin));
            ValidateStringLength(Pin, nameof(Pin), 4, 12);
            ValidateDigitsOnly(Pin, nameof(Pin));

            ValidateRequired(ExpiryYYMM, nameof(ExpiryYYMM));
            ValidateExactLength(ExpiryYYMM, nameof(ExpiryYYMM), 4);
            ValidateDigitsOnly(ExpiryYYMM, nameof(ExpiryYYMM));

            ValidatePositive(AmountMinor, nameof(AmountMinor));

            ValidateRequired(Currency, nameof(Currency));
            ValidateExactLength(Currency, nameof(Currency), 3);
            ValidateLettersOnly(Currency, nameof(Currency));
        }
    }   
}
