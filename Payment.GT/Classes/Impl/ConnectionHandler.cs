using Microsoft.Extensions.Logging;
using Payment.GT.Classes.Interface;
using Payment.Protocol;
using Payment.Protocol.Interface;
using System.IO.Pipelines;
using System.Net.Sockets;

namespace Payment.GT.Classes.Impl
{
    public sealed class ConnectionHandler 
    {
        private readonly TcpClient _client;
        private readonly IGatewayProcessor _processor;
        private readonly ILogger<ConnectionHandler> _log;
        private readonly IFrameOperator _frameOperator;

        public ConnectionHandler(TcpClient client, IGatewayProcessor processor, IFrameOperator frameOperator, ILogger<ConnectionHandler> log)
        {
            _client = client;
            _processor = processor;
            _log = log;
            _frameOperator = frameOperator;
        }

        public async Task RunAsync(CancellationToken ct)
        {
            await using var stream = _client.GetStream();
            var reader = PipeReader.Create(stream);

            try
            {
                while (!ct.IsCancellationRequested)
                {
                    var readResult = await reader.ReadAsync(ct);
                    var buffer = readResult.Buffer;
                    try
                    {
                        while (_frameOperator.BinaryToFrame(ref buffer, out var frame))
                        {
                            byte[]? response;
                            response = await _processor.HandleAsync(frame, ct);

                            if (response is null)
                            {
                                _log.LogWarning("Unsupported msgType=0x{MsgType:X2}; closing connection", frame.MsgType);
                                return;
                            }
                            await stream.WriteAsync(response, ct);
                        }
                    }
                    catch (Exception ex)
                    {
                        _log.LogError(ex, "Unhandled error while processing message");
                        return; 
                    }

                    reader.AdvanceTo(buffer.Start, buffer.End);
                    if (readResult.IsCompleted)
                        break;
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                // shutdown
            }
            catch (IOException ex)
            {
                _log.LogInformation(ex, "Client disconnected");
            }
            finally
            {
                await reader.CompleteAsync();
                try { _client.Close(); } catch { }
            }
        }
    }
}
