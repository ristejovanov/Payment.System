using Microsoft.Extensions.Logging;
using Payment.Protocol;
using System.IO.Pipelines;
using System.Net.Sockets;

namespace Payment.GT.Classes.Impl
{
    public sealed class ConnectionHandler
    {
        private readonly TcpClient _client;
        private readonly GatewayProcessor _processor;
        private readonly ILogger<ConnectionHandler> _log;

        public ConnectionHandler(TcpClient client, GatewayProcessor processor, ILogger<ConnectionHandler> log)
        {
            _client = client;
            _processor = processor;
            _log = log;
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

                    while (FrameParser.TryReadFrame(ref buffer, out var payload))
                    {
                        ParsedFrame frame;
                        try
                        {
                            frame = FrameCodec.Parse(payload);
                        }
                        catch (Exception ex)
                        {
                            _log.LogWarning(ex, "Protocol parse error; closing connection");
                            return; // protocol violation -> close
                        }

                        byte[]? response;
                        try
                        {
                            response = await _processor.HandleAsync(frame, ct);
                        }
                        catch (Exception ex)
                        {
                            _log.LogError(ex, "Unhandled error processing msgType=0x{MsgType:X2}; closing connection", frame.MsgType);
                            return; // safest in a payment-like gateway: close on unexpected handler exceptions
                        }

                        if (response is null)
                        {
                            _log.LogWarning("Unsupported msgType=0x{MsgType:X2}; closing connection", frame.MsgType);
                            return;
                        }

                        await stream.WriteAsync(response, ct);
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
