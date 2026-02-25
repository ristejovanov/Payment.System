using Microsoft.Extensions.Logging;
using Payment.Protocol.Impl.Base;
using Payment.Protocol.Interface;
using System.Runtime.InteropServices.JavaScript;

namespace Payment.Protocol.Impl
{
    public sealed class ObjectCreator : IObjectCreator
    {
        private readonly ILogger<ObjectCreator> _logger;
        private readonly ITlvMapper _mapper;
        private readonly IFrameOperator _frameOperator;

        public ObjectCreator(
            ILogger<ObjectCreator> logger,
            ITlvMapper mapper,
            IFrameOperator frameOperator)
        {
            _logger = logger;
            _mapper = mapper;
            _frameOperator = frameOperator;
        }

        public byte[] ToBytes(object obj, bool skipEmptyStrings = true, bool skipDefaultNumbers = true)
        {
            if (obj is null) throw new ArgumentNullException(nameof(obj));

            if (obj is not MessageBase typed)
                throw new InvalidOperationException(
                    $"Object of type {obj.GetType().Name} must implement IHasMsgType to be encoded.");

            var tlvs = _mapper.ToTlvs(obj, skipEmptyStrings, skipDefaultNumbers);
            var frame = _frameOperator.FrameToBinary(new Frame { MsgType = typed.MsgType, Version = typed.Version, Tlvs = tlvs });

            _logger.LogDebug("Encoded {Type} to frame bytes. msgType=0x{MsgType:X2}, tlvs={Count}, len={Len}",
                obj.GetType().Name, typed.MsgType, tlvs.Count, frame.Length);

            return frame;
        }

        public object ToObject(Frame frame)
        {
            



            if ( is not MessageBase typed)
                throw new InvalidOperationException(
                    $"Object of type {obj.GetType().Name} must implement IHasMsgType to be encoded.");

            var tlvs = _mapper.ToTlvs(obj, skipEmptyStrings, skipDefaultNumbers);
            var frame = _frameOperator.FrameToBinary(new Frame { MsgType = typed.MsgType, Version = typed.Version, Tlvs = tlvs });

            _logger.LogDebug("Encoded {Type} to frame bytes. msgType=0x{MsgType:X2}, tlvs={Count}, len={Len}",
                obj.GetType().Name, typed.MsgType, tlvs.Count, frame.Length);

            return frame;
        }
    }
}
