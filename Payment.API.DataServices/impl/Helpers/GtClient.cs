using Microsoft.Extensions.Logging;
using Payment.API.DataServices.impl.Helpers;
using Payment.API.DataServices.interfaces.Helpers;
using Payment.Protocol;
using Payment.Protocol.Dtos;
using Payment.Protocol.Interface;
using Payment.Shared.Dto;
using System.Collections.Concurrent;
using System.Data;
using System.Net.Sockets;

public sealed class GtClient : IGtClient
{
    private readonly IGtConnection _conn;
    private readonly IObjectCreator _objectCreator;
    private readonly GtClientOptions _opt;
    private readonly ILogger<GtClient> _log;

    private readonly ConcurrentDictionary<string, TaskCompletionSource<Frame>> _pending =
        new(StringComparer.Ordinal);

    public GtClient(IGtConnection conn, IObjectCreator objectCreator, GtClientOptions opt, ILogger<GtClient> log)
    {
        _conn = conn;
        _objectCreator = objectCreator;
        _opt = opt;
        _log = log;

        _conn.FrameReceived += OnFrame;
    }

    private void OnFrame(Frame frame)
    {
        var corr = frame.GetAsciiOrNull(Tags.CorrelationId);
        if (corr is null) return;

        if (_pending.TryRemove(corr, out var tcs))
            tcs.TrySetResult(frame);
    }

    public async Task<Frame> SendAndWaitWithRetryAsync(RequestDto req, CancellationToken ct)
    {
        // Create ONCE
        var tcs = new TaskCompletionSource<Frame>(TaskCreationOptions.RunContinuationsAsynchronously);
        if (!_pending.TryAdd(req.CorrelationId, tcs))
            throw new InvalidOperationException("Duplicate correlationId in-flight.");

        try
        {
            for (int attempt = 0; attempt <= _opt.MaxRetries; attempt++)
            {
                req.IsRepeat = attempt > 0;

                var bytes = _objectCreator.ToBytes(req);
                await _conn.EnsureConnectedAsync(ct);

                try
                {
                    await _conn.SendAsync(bytes, ct);

                    using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                    timeoutCts.CancelAfter(_opt.TimeoutMs);

                    return await tcs.Task.WaitAsync(timeoutCts.Token);
                }
                catch (OperationCanceledException) when (!ct.IsCancellationRequested)
                {
                    if (attempt >= _opt.MaxRetries)
                        throw new TimeoutException("GT did not respond after retries.");

                    continue;
                }
                catch (Exception ex) when (IsConnectionException(ex) && attempt < _opt.MaxRetries)
                {
                    _log.LogWarning(ex, "Connection error, reconnecting");
                    await _conn.ForceReconnectAsync(ct);
                    continue;
                }
            }

            throw new TimeoutException("GT did not respond after retries.");
        }
        finally
        {
            _pending.TryRemove(req.CorrelationId, out _);
        }
    }

    private static bool IsConnectionException(Exception ex)
        => ex is IOException or SocketException or ObjectDisposedException;
}
