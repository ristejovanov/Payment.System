using Payment.API.DataServices.impl.Helpers;
using Payment.Protocol;
using Payment.Protocol.Base;
using Payment.Shared.Dto;
using System.Collections.Concurrent;
using System.IO.Pipelines;
using System.Net.Sockets;
using System.Text;

namespace AtmService.Tcp;

public sealed class GtTcpClient : IGtClient 
{
    private readonly string _host;
    private readonly int _port;

    private TcpClient? _tcp;
    private NetworkStream? _stream;
    private PipeReader? _reader;

    private readonly GtClientOptions _opt;
    private readonly SemaphoreSlim _sendLock = new(1, 1);
    private readonly CancellationTokenSource _cts = new();
    private Task? _readTask;
    private Task? _heartbeatTask;

    private readonly SemaphoreSlim _pongGate = new(1, 1);
    private TaskCompletionSource<bool>? _pongTcs;


    private readonly ConcurrentDictionary<string, TaskCompletionSource<ParsedFrame>> _pending =
        new(StringComparer.Ordinal);

    private long _stan = 184000;

    public GtTcpClient(string host, int port)
    {
        _host = host;
        _port = port;
    }

    public Task StartAsync(CancellationToken ct) => EnsureConnectedAsync(ct);

    // --- Public API used by controller ---
    public async Task<ParsedFrame> SendAndWaitWithRetryAsync(byte msgType, IReadOnlyList<Tlv> tlvs, string correlationId, CancellationToken ct)
    {
        //Create ONCE
        var tcs = new TaskCompletionSource<ParsedFrame>(TaskCreationOptions.RunContinuationsAsynchronously);
        if (!_pending.TryAdd(correlationId, tcs))
            throw new InvalidOperationException("Duplicate correlationId in-flight.");

        try
        {
            for (int attempt = 0; attempt <= _opt.MaxRetries; attempt++)
            {
                await EnsureConnectedAsync(ct);                
                var actualTlvs = (attempt > 0)? SetIsRepeat(tlvs, "0"): SetIsRepeat(tlvs, "1");

                try
                {
                    await SendFrameAsync(msgType, actualTlvs, ct);

                    using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                    timeoutCts.CancelAfter(_opt.TimeoutMs);

                    // NOTE: WaitAsync throws OperationCanceledException on timeout.
                    return await tcs.Task.WaitAsync(timeoutCts.Token);
                }
                catch (OperationCanceledException) when (!ct.IsCancellationRequested)
                {
                    if (attempt > _opt.MaxRetries)
                        throw new TimeoutException("GT did not respond after retries.");

                    continue;
                }
                catch (Exception ex) when (IsConnectionException(ex) && attempt <= _opt.MaxRetries)
                {
                    // Connection problem -> reconnect -> retry
                    await ForceReconnectAsync(ct);
                    continue;
                }
            }

            // last attempt timed out or failed
            throw new TimeoutException("GT did not respond after retries.");
        }
        finally
        {
            // Remove once
            _pending.TryRemove(correlationId, out _);
        }
    }


    // --- Connection / loops ---
    private async Task EnsureConnectedAsync(CancellationToken ct)
    {
        if (_tcp is not null && _stream is not null && _readTask is not null && !_readTask.IsCompleted)
            return;

        await ForceReconnectAsync(ct);
    }

    private async Task ForceReconnectAsync(CancellationToken ct)
    {
        FailAllPending(new IOException("Reconnecting"));

        try { _cts.Token.ThrowIfCancellationRequested(); } catch { }

        SafeDisposeConnection();

        _tcp = new TcpClient();
        await _tcp.ConnectAsync(_host, _port, ct);
        _stream = _tcp.GetStream();
        _reader = PipeReader.Create(_stream);

        _readTask = Task.Run(ReadLoopAsync, _cts.Token);
        _heartbeatTask ??= Task.Run(HeartbeatLoopAsync, _cts.Token);
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
                    while (FrameParser.TryReadFrame(ref buffer, out var frame))
                    {
                        if (frame!.Version != MessageTypes.Version) continue;
                        if (frame.MsgType == MessageTypes.Pong){
                            _pongTcs?.TrySetResult(true);
                            continue;
                        }
                        var corr = frame.GetAsciiOrNull(Tags.CorrelationId);
                        if (corr is null) continue;

                        if (_pending.TryRemove(corr, out var tcs))
                            tcs.TrySetResult(frame);
                        // else: late/untracked (ignore)
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
            FailAllPending(ex);
            SafeDisposeConnection(); // will trigger reconnect on next send/heartbeat
        }
    }

    // --- Heartbeat ---
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

                // Important: don’t overlap heartbeat with an active request send
                // Use the same send lock you use for normal frames
                using var timeoutCts = new CancellationTokenSource(timeout);
                using var linked = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token, timeoutCts.Token);

                await SendPingAndWaitPongAsync(linked.Token);
            }
            catch (OperationCanceledException) when (_cts.IsCancellationRequested)
            {
                break;
            }
            catch
            {
                // best effort reconnect
                try { await ForceReconnectAsync(_cts.Token); } catch { }
            }
        }
    }



    private async Task SendPingAndWaitPongAsync(CancellationToken ct)
    {
        // Ensure only one ping is in-flight
        await _pongGate.WaitAsync(ct);
        try
        {
            _pongTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            // Send ping frame (no TLVs)
            var pingFrame = FrameWriter.BuildFrame(msgType: MessageTypes.Ping, tlvs: Array.Empty<Tlv>());

            await _sendLock.WaitAsync(ct);
            try
            {
                await _stream!.WriteAsync(pingFrame, ct);
            }
            finally
            {
                _sendLock.Release();
            }

            // Wait for read loop to signal pong
            await _pongTcs.Task.WaitAsync(ct);
        }
        finally
        {
            _pongTcs = null;
            _pongGate.Release();
        }
    }

    private async Task SendFrameAsync(byte msgType, IReadOnlyList<Tlv> tlvs, CancellationToken ct)
    {
        var frame = FrameWriter.BuildFrame(msgType, tlvs);

        await _sendLock.WaitAsync(ct);
        try
        {
            await _stream!.WriteAsync(frame, ct);
            await _stream.FlushAsync(ct);
        }
        finally
        {
            _sendLock.Release();
        }
    }

    private static IReadOnlyList<Tlv> SetIsRepeat(IReadOnlyList<Tlv> tlvs, string val)
    {
        var bytes = Encoding.ASCII.GetBytes(val);
        var list = tlvs.ToList();
        var idx = list.FindIndex(t => t.Tag == Tags.IsRepeat);
        if (idx >= 0) list[idx] = new Tlv(Tags.IsRepeat, bytes);
        else list.Add(new Tlv(Tags.IsRepeat, bytes));
        return list;
    }

    public string NextStan() => Interlocked.Increment(ref _stan).ToString();

    private void FailAllPending(Exception ex)
    {
        foreach (var kv in _pending)
            if (_pending.TryRemove(kv.Key, out var tcs))
                tcs.TrySetException(ex);
    }

    private static string? GetAscii(ParsedFrame frame, byte tag)
    {
        var tlv = frame.Tlvs.FirstOrDefault(t => t.Tag == tag);
        return tlv.Value.IsEmpty ? null : FrameWriter.Ascii(tlv.Value);
    }

    public async ValueTask DisposeAsync()
    {
        _cts.Cancel();
        FailAllPending(new IOException("Shutting down"));
        try { if (_readTask is not null) await _readTask; } catch { }
        try { if (_heartbeatTask is not null) await _heartbeatTask; } catch { }
        SafeDisposeConnection();
        _sendLock.Dispose();
        _cts.Dispose();
    }


    private bool IsConnectionException(Exception ex)
    {
        // If the caller asked to cancel, don't treat it as a connection failure.
        if (ct.IsCancellationRequested)
            return false;

        // Unwrap AggregateException (common with tasks)
        if (ex is AggregateException ae)
            ex = ae.GetBaseException();

        // If the exception (or its base) is cancellation, it might still be connection-related
        // (e.g., PipeReader can throw OperationCanceledException on disconnect),
        // but we only treat cancellation as connection failure when OUR ct isn't canceled.
        if (ex is OperationCanceledException oce)
        {
            // If it's linked to our ct -> not connection failure
            if (oce.CancellationToken == ct)
                return false;

            // Otherwise could be a pipeline/stream cancellation due to disconnect -> treat as connection failure
            return true;
        }

        // Direct types commonly indicating transport failure
        if (ex is SocketException)
            return true;

        if (ex is IOException)
            return true;

        if (ex is ObjectDisposedException)
            return true;

        if (ex is EndOfStreamException)
            return true;

        // Many IOExceptions wrap SocketException
        if (ex.InnerException is SocketException)
            return true;

        if (ex.InnerException is IOException)
            return true;

        return false;
    }
}