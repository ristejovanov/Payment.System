using Microsoft.Extensions.Logging;
using Payment.API.DataServices.interfaces.Helpers;
using Payment.Protocol;
using Payment.Protocol.Interface;
using System.IO.Pipelines;
using System.Net.Sockets;

namespace Payment.API.DataServices.impl.Helpers
{
    public sealed class GtConnection : IGtConnection
    {
        private readonly string _host;
        private readonly int _port;
        private readonly IFrameOperator _frameOperator;
        private readonly ILogger<GtConnection> _log;

        private TcpClient? _tcp;
        private NetworkStream? _stream;
        private PipeReader? _reader;

        private readonly SemaphoreSlim _sendLock = new(1, 1);
        private readonly CancellationTokenSource _cts = new();

        private Task? _readTask;
        private Task? _heartbeatTask;

        // heartbeat coordination
        private readonly SemaphoreSlim _pongGate = new(1, 1);
        private TaskCompletionSource<bool>? _pongTcs;

        public event Action<Frame>? FrameReceived;

        public GtConnection(string host, int port, IFrameOperator frameOperator, ILogger<GtConnection> log)
        {
            _host = host;
            _port = port;
            _frameOperator = frameOperator;
            _log = log;
        }

        public async Task EnsureConnectedAsync(CancellationToken ct)
        {
            if (_tcp is not null && _stream is not null && _readTask is not null && !_readTask.IsCompleted)
                return;

            await ForceReconnectAsync(ct);
        }

        public async Task ForceReconnectAsync(CancellationToken ct)
        {
            SafeDisposeConnection();

            _tcp = new TcpClient();
            await _tcp.ConnectAsync(_host, _port, ct);
            _stream = _tcp.GetStream();
            _reader = PipeReader.Create(_stream);

            _readTask = Task.Run(ReadLoopAsync, _cts.Token);
            _heartbeatTask ??= Task.Run(HeartbeatLoopAsync, _cts.Token);

            _log.LogInformation("GT connected to {Host}:{Port}", _host, _port);
        }

        public async Task SendAsync(byte[] frameBytes, CancellationToken ct)
        {
            await EnsureConnectedAsync(ct);

            await _sendLock.WaitAsync(ct);
            try
            {
                await _stream!.WriteAsync(frameBytes, ct);
                await _stream.FlushAsync(ct);
            }
            finally
            {
                _sendLock.Release();
            }
        }

        private async Task ReadLoopAsync()
        {
            try
            {
                while (!_cts.IsCancellationRequested)
                {
                    var result = await _reader!.ReadAsync(_cts.Token);
                    var buffer = result.Buffer;

                    try
                    {
                        while (_frameOperator.BinaryToFrame(ref buffer, out var frame))
                        {
                            if (frame!.Version != MessageTypes.Version)
                                continue;

                            if (frame.MsgType == MessageTypes.Pong)
                            {
                                _pongTcs?.TrySetResult(true);
                                continue;
                            }

                            FrameReceived?.Invoke(frame);
                        }

                        _reader.AdvanceTo(buffer.Start, buffer.End);
                    }
                    catch
                    {
                        _reader.AdvanceTo(buffer.End);
                        throw;
                    }

                    if (result.IsCompleted)
                        throw new IOException("GT disconnected");
                }
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "GT read loop stopped");
                SafeDisposeConnection(); // trigger reconnect on next send
            }
        }

        private async Task HeartbeatLoopAsync()
        {
            var interval = TimeSpan.FromSeconds(15);
            var timeout = TimeSpan.FromSeconds(2);

            while (!_cts.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(interval, _cts.Token);
                    await EnsureConnectedAsync(_cts.Token);

                    using var timeoutCts = new CancellationTokenSource(timeout);
                    using var linked = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token, timeoutCts.Token);

                    await SendPingAndWaitPongAsync(linked.Token);
                }
                catch (OperationCanceledException) when (_cts.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _log.LogWarning(ex, "Heartbeat failed, reconnecting");
                    try { await ForceReconnectAsync(_cts.Token); } catch { }
                }
            }
        }

        private async Task SendPingAndWaitPongAsync(CancellationToken ct)
        {
            await _pongGate.WaitAsync(ct);
            try
            {
                _pongTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

                var ping = _frameOperator.FrameToBinary(new Frame
                {
                    MsgType = MessageTypes.Ping,
                    Version = MessageTypes.Version,
                    Tlvs = Array.Empty<Tlv>()
                });

                await SendAsync(ping, ct);
                await _pongTcs.Task.WaitAsync(ct);
            }
            finally
            {
                _pongTcs = null;
                _pongGate.Release();
            }
        }

        private void SafeDisposeConnection()
        {
            try { _reader?.Complete(); } catch { }
            try { _stream?.Dispose(); } catch { }
            try { _tcp?.Dispose(); } catch { }

            _reader = null;
            _stream = null;
            _tcp = null;
            _readTask = null;
        }

        public async ValueTask DisposeAsync()
        {
            _cts.Cancel();
            try { if (_readTask is not null) await _readTask; } catch { }
            try { if (_heartbeatTask is not null) await _heartbeatTask; } catch { }
            SafeDisposeConnection();
            _sendLock.Dispose();
            _cts.Dispose();
            _pongGate.Dispose();
        }
    }

}
