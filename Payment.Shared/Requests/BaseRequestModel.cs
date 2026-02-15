using System.ComponentModel.DataAnnotations;

namespace Payment.Shared.Requests
{
    /// <summary>
    /// Base class for request models that provides built-in validation capabilities.
    /// Derived classes must implement custom validation logic by overriding the ValidateModel method.
    /// </summary>
    public abstract class BaseRequestModel : IValidatableObject
    {
        private readonly List<ValidationResult> _errors = new();

        /// <summary>
        /// Validates the model and returns any validation errors.
        /// This method is called automatically by the validation framework.
        /// </summary>
        /// <param name="validationContext">The context for the validation operation.</param>
        /// <returns>A collection of validation results.</returns>
        IEnumerable<ValidationResult> IValidatableObject.Validate(ValidationContext validationContext)
        {
            _errors.Clear();
            ValidateModel(validationContext);
            return _errors;
        }

        /// <summary>
        /// When overridden in a derived class, performs custom validation logic for the model.
        /// Use the provided helper methods to add validation errors.
        /// </summary>
        /// <param name="validationContext">The context for the validation operation.</param>
        protected abstract void ValidateModel(ValidationContext validationContext);

        #region Helper Methods
        
        /// <summary>
        /// Adds a field-specific validation error.
        /// </summary>
        /// <param name="fieldName">The name of the field with the validation error.</param>
        /// <param name="message">The validation error message.</param>
        protected void AddError(string fieldName, string message)
                => _errors.Add(new ValidationResult(message, new[] { fieldName }));
        
        /// <summary>
        /// Adds a model-level validation error that is not specific to a single field.
        /// </summary>
        /// <param name="message">The validation error message.</param>
        protected void AddModelError(string message)
            => _errors.Add(new ValidationResult(message));
        
        /// <summary>
        /// Validates that a required string field has a non-null, non-empty value.
        /// </summary>
        /// <param name="value">The value to validate.</param>
        /// <param name="fieldName">The name of the field being validated.</param>
        protected void ValidateRequired(string? value, string fieldName)
        {
            if (string.IsNullOrWhiteSpace(value))
                AddError(fieldName, $"{fieldName} is required.");
        }

        /// <summary>
        /// Validates that a string field's length falls within the specified range.
        /// </summary>
        /// <param name="value">The value to validate.</param>
        /// <param name="fieldName">The name of the field being validated.</param>
        /// <param name="minLength">The minimum allowed length.</param>
        /// <param name="maxLength">The maximum allowed length.</param>
        protected void ValidateStringLength(string? value, string fieldName, int minLength, int maxLength)
        {
            if (string.IsNullOrWhiteSpace(value)) return;

            if (value.Length < minLength || value.Length > maxLength)
                AddError(fieldName, $"{fieldName} must be between {minLength} and {maxLength} characters.");
        }

        /// <summary>
        /// Validates that a string field has an exact length.
        /// </summary>
        /// <param name="value">The value to validate.</param>
        /// <param name="fieldName">The name of the field being validated.</param>
        /// <param name="length">The required exact length.</param>
        protected void ValidateExactLength(string? value, string fieldName, int length)
        {
            if (string.IsNullOrWhiteSpace(value)) return;

            if (value.Length != length)
                AddError(fieldName, $"{fieldName} must be exactly {length} characters.");
        }

        /// <summary>
        /// Validates that a string field contains only numeric digits (0-9).
        /// </summary>
        /// <param name="value">The value to validate.</param>
        /// <param name="fieldName">The name of the field being validated.</param>
        protected void ValidateDigitsOnly(string? value, string fieldName)
        {
            if (string.IsNullOrWhiteSpace(value)) return;

            if (!value.All(char.IsDigit))
                AddError(fieldName, $"{fieldName} must contain digits only.");
        }

        /// <summary>
        /// Validates that a string field contains only alphabetic letters.
        /// </summary>
        /// <param name="value">The value to validate.</param>
        /// <param name="fieldName">The name of the field being validated.</param>
        protected void ValidateLettersOnly(string? value, string fieldName)
        {
            if (string.IsNullOrWhiteSpace(value)) return;

            if (!value.All(char.IsLetter))
                AddError(fieldName, $"{fieldName} must contain letters only.");
        }

        /// <summary>
        /// Validates that a numeric value is positive (greater than 0).
        /// </summary>
        /// <param name="value">The value to validate.</param>
        /// <param name="fieldName">The name of the field being validated.</param>
        protected void ValidatePositive(long value, string fieldName)
        {
            if (value <= 0)
                AddError(fieldName, $"{fieldName} must be greater than 0.");
        }

        /// <summary>
        /// Validates that a numeric value falls within the specified range (inclusive).
        /// </summary>
        /// <param name="value">The value to validate.</param>
        /// <param name="fieldName">The name of the field being validated.</param>
        /// <param name="min">The minimum allowed value (inclusive).</param>
        /// <param name="max">The maximum allowed value (inclusive).</param>
        protected void ValidateRange(long value, string fieldName, long min, long max)
        {
            if (value < min || value > max)
                AddError(fieldName, $"{fieldName} must be between {min} and {max}.");
        }

        #endregion
    }
}
