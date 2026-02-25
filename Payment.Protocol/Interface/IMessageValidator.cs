using Payment.Protocol.Impl;

namespace Payment.Protocol.Interface
{
    public interface IMessageValidator<in TDto>
    {
        ValidationResult Validate(TDto dto);
    }
}
