using Payment.API.DataServices.interfaces.Helpers;

namespace Payment.API.DataServices.impl.Helpers
{
    public sealed class StanGenerator : IStenGenerator
    {
        private long _value = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        public long Next() => Interlocked.Increment(ref _value);
    }
}
