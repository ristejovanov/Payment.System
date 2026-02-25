using System;
using System.Collections.Generic;
using System.Text;

namespace Payment.Protocol.Impl
{
    public record ValidationError(string Field, string Code, string Message);

    public class ValidationResult
    {
        private readonly List<ValidationError> _errors = new();
        public IReadOnlyList<ValidationError> Errors => _errors;
        public bool IsValid => _errors.Count == 0;

        public void Add(string field, string code, string message)
            => _errors.Add(new ValidationError(field, code, message));

        public override string ToString()
            => string.Join("; ", _errors.Select(e => $"{e.Field}:{e.Code}:{e.Message}"));
    }
}
