using Payment.Protocol.Dto;
using Payment.Protocol.Impl;
using Payment.Protocol.Interface;

namespace Payment.Protocol.DtoValidations
{
    public sealed class A70RequestValidator : IMessageValidator<A70RequestDto>
    {
        public ValidationResult Validate(A70RequestDto dto)
        {
            var r = new ValidationResult();

            if (string.IsNullOrWhiteSpace(dto.AtmId))
                r.Add(nameof(dto.AtmId), "MISSING", "Atm Id is required");

            if (string.IsNullOrWhiteSpace(dto.CorrelationId))
                r.Add(nameof(dto.CorrelationId), "MISSING", "Correlation id is required");

            if (dto.Stan <= 0 || dto.Stan == 0)
                r.Add(nameof(dto.Stan), "BAD", "Invalid Stan number");

            if (string.IsNullOrWhiteSpace(dto.Pan))
                r.Add(nameof(dto.Pan), "MISSING", "Pan is reqquired");
            else if (dto.Pan.Length < 12 || dto.Pan.Length > 19 || !dto.Pan.All(char.IsDigit))
                r.Add(nameof(dto.Pan), "BAD", "Pan is invalid");

            if (string.IsNullOrWhiteSpace(dto.PinBlock))
                r.Add(nameof(dto.PinBlock), "MISSING", "Pinblock is required");

            if (dto.AmountMinor <= 0)
                r.Add(nameof(dto.AmountMinor), "BAD", "Amount must be positive");

            if (string.IsNullOrWhiteSpace(dto.Currency))
                r.Add(nameof(dto.Currency), "MISSING", "Currency required");

            return r;
        }
    }
}
