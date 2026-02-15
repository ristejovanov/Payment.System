using Payment.Shared.Enums;
using System.ComponentModel.DataAnnotations;

namespace Payment.Shared.Requests
{
    public class CompleteWithdrawalRequest : BaseRequestModel
    {
        public required string AtmId { get; set; }
        public required string CorrelationId { get; set; }

        public required long OriginalStan { get; set; }

        public required DispenseResultType DispenseResult { get; set; }

        public required int DispensedAmountMinor { get; set; }


        protected override void ValidateModel(ValidationContext validationContext)
        {
            // Required strings
            ValidateRequired(AtmId, nameof(AtmId));
            ValidateRequired(CorrelationId, nameof(CorrelationId));

            // OriginalStan is numeric now
            ValidatePositive(OriginalStan, nameof(OriginalStan));

            // Enum must be valid (protects against invalid numeric values coming from JSON)
            if (!Enum.IsDefined(typeof(DispenseResultType), DispenseResult))
                AddError(nameof(DispenseResult), "DispenseResult must be one of: OK, PARTIAL, FAILED.");

            // Amount cannot be negative
            if (DispensedAmountMinor < 0)
                AddError(nameof(DispensedAmountMinor), "DispensedAmountMinor cannot be negative.");

            // Optional domain consistency checks (recommended)
            if (DispenseResult == DispenseResultType.FAILED && DispensedAmountMinor != 0)
                AddError(nameof(DispensedAmountMinor), "When DispenseResult is FAILED, DispensedAmountMinor must be 0.");

            if ((DispenseResult == DispenseResultType.OK || DispenseResult == DispenseResultType.PARTIAL) &&
                DispensedAmountMinor <= 0)
                AddError(nameof(DispensedAmountMinor), "When DispenseResult is OK/PARTIAL, DispensedAmountMinor must be greater than 0.");
        }
    }
}