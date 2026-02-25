using Payment.Protocol.Dtos;
using Payment.Protocol.Impl;
using Payment.Protocol.Interface;

namespace Payment.Protocol.DtoValidations
{
    public sealed class A72RequestValidator : IMessageValidator<A72RequestDto>
    {
        public ValidationResult Validate(A72RequestDto dto)
        {
            var r = new ValidationResult();

            if (string.IsNullOrWhiteSpace(dto.AtmId))
                r.Add(nameof(dto.AtmId), "MISSING", "Atm Id is required");

            if (dto.Stan <= 0 || dto.Stan == 0)
                r.Add(nameof(dto.Stan), "BAD", "Invalid Stan number");

            if (string.IsNullOrWhiteSpace(dto.CorrelationId))
                r.Add(nameof(dto.CorrelationId), "MISSING", "Correlation id is required");

            if (dto.OriginalStan <= 0 || dto.OriginalStan == 0)
                r.Add(nameof(dto.Stan), "BAD", "Invalid  OriginalStan number");

            return r;
        }
    }

}
