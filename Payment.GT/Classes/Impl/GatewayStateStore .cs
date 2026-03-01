using Microsoft.Extensions.Caching.Memory;
using Payment.GT.Classes.Interface;
using Payment.Protocol.Dto;
using Payment.Shared.Dto;
using Payment.Shared.Enums;
using System.Collections.Concurrent;

namespace Payment.GT.Classes.Impl
{
    public sealed class GatewayStateStore : IGatewayStateStore
    {
        private readonly IMemoryCache _cache;

        // Lock striping per reservation key (prevents races cleanly)
        private readonly ConcurrentDictionary<(string atmId, long stan), object> _locks = new();

        // TTL knobs (tune as you like)
        private readonly TimeSpan _reservationTtl = TimeSpan.FromMinutes(5);
        private readonly TimeSpan _responseTtl = TimeSpan.FromMinutes(5);

        public GatewayStateStore(IMemoryCache cache)
        {
            _cache = cache;
        }

        private static string ResKey(string atmId, long stan) => $"res:{atmId}:{stan}";
        private static string A73Key(string atmId, long originalStan) => $"a73:{atmId}:{originalStan}";

        public BeginReservationResult BeginReservation(A70RequestDto request, string fingerprint)
        {
            var lockKey = (request.AtmId, request.Stan);
            var gate = _locks.GetOrAdd(lockKey, _ => new object());

            lock (gate)
            {
                if (_cache.TryGetValue(ResKey(request.AtmId, request.Stan), out ReservationEntry existing))
                {
                    // Fingerprint consistency check
                    if (!string.Equals(existing.Record.Fingerprint, fingerprint, StringComparison.Ordinal))
                    {
                        return new BeginReservationResult(
                            Outcome: BeginReservationOutcome.StanReuseDifferentPayload,
                            Record: existing.Record,
                            CachedA71Bytes: null,
                            ErrorMessage: "Stan reuse different payload");
                    }

                    // Completed? return cached bytes
                    if (existing.A71Bytes is not null)
                    {
                        return new BeginReservationResult(
                            Outcome: BeginReservationOutcome.AlreadyCompleted,
                            Record: existing.Record,
                            CachedA71Bytes: Clone(existing.A71Bytes),
                            ErrorMessage: null);
                    }

                    return new BeginReservationResult(
                        Outcome: BeginReservationOutcome.AlreadyProcessing,
                        Record: existing.Record,
                        CachedA71Bytes: null,
                        ErrorMessage: null);
                }

                // Start new reservation in Processing state
                var record = new ReservationRecord(
                    AtmId: request.AtmId,
                    Stan: request.Stan,
                    CorrelationId: request.CorrelationId,
                    ReservedAmountMinor: request.AmountMinor,
                    Currency: request.Currency,
                    Fingerprint: fingerprint,
                    Status: ReservationStatus.Processing,
                    Rc: "09",
                    AuthCode: null,
                    Message: "PENDING",
                    CreatedUtc: DateTime.UtcNow,
                    CompletedUtc: null);

                var entry = new ReservationEntry(record, null);

                // Cache entry with TTL + eviction callback to cleanup secondary structures
                var options = new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = _reservationTtl,
                    Priority = CacheItemPriority.Normal
                }.RegisterPostEvictionCallback((key, value, reason, state) =>
                {
                    if (value is ReservationEntry e)
                    {
                        // remove lock gate
                        _locks.TryRemove((e.Record.AtmId, e.Record.Stan), out _);
                    }
                });

                _cache.Set(ResKey(request.AtmId, request.Stan), entry, options);

                return new BeginReservationResult(
                    Outcome: BeginReservationOutcome.StartedNew,
                    Record: record,
                    CachedA71Bytes: null,
                    ErrorMessage: null);
            }
        }

        public void CompleteReservation(CompleteReservationRequest request)
        {
            var lockKey = (request.AtmId, request.Stan);
            var gate = _locks.GetOrAdd(lockKey, _ => new object());

            lock (gate)
            {
                var resKey = ResKey(request.AtmId, request.Stan);

                // Ensure exists; if missing, create (still deterministic)
                if (!_cache.TryGetValue(resKey, out ReservationEntry existing))
                {
                    var rec = new ReservationRecord(
                        AtmId: request.AtmId,
                        Stan: request.Stan,
                        CorrelationId: request.CorrelationId,
                        ReservedAmountMinor: request.AmountMinor,
                        Currency: request.Currency,
                        Fingerprint: request.Fingerprint,
                        Status: request.Rc == "00" ? ReservationStatus.Approved
                              : request.Rc == "91" ? ReservationStatus.Timeout
                              : ReservationStatus.Declined,
                        Rc: request.Rc,
                        AuthCode: request.AuthCode,
                        Message: request.Message,
                        CreatedUtc: request.CompletedUtc,
                        CompletedUtc: request.CompletedUtc);

                    var entry = new ReservationEntry(rec, Clone(request.A71Bytes));

                    // Keep correlation index

                    // Store with TTL; since it's completed, we can use response TTL (or keep reservation TTL)
                    var option = new MemoryCacheEntryOptions
                    {
                        AbsoluteExpirationRelativeToNow = _responseTtl,
                        Priority = CacheItemPriority.Normal
                    }.RegisterPostEvictionCallback((key, value, reason, state) =>
                    {
                        if (value is ReservationEntry e)
                        { 
                            _locks.TryRemove((e.Record.AtmId, e.Record.Stan), out _);
                        }
                    });

                    _cache.Set(resKey, entry, option);
                    return;
                }

                // Fingerprint mismatch: keep original; do not overwrite
                if (!string.Equals(existing.Record.Fingerprint, request.Fingerprint, StringComparison.Ordinal))
                    return;

                // If already has bytes, keep first bytes (deterministic replay)
                if (existing.A71Bytes is not null)
                    return;

                // Update record + store exact bytes
                var updatedRec = existing.Record with
                {
                    // keep original correlationId
                    Rc = request.Rc,
                    AuthCode = request.AuthCode,
                    Message = request.Message,
                    CompletedUtc = request.CompletedUtc,
                    Status = request.Rc == "00" ? ReservationStatus.Approved
                          : request.Rc == "91" ? ReservationStatus.Timeout
                          : ReservationStatus.Declined
                };

                var updatedEntry = new ReservationEntry(updatedRec, Clone(request.A71Bytes));

                // Upgrade TTL to response TTL now that it’s completed
                var options = new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = _responseTtl,
                    Priority = CacheItemPriority.Normal
                }.RegisterPostEvictionCallback((key, value, reason, state) =>
                {
                    if (value is ReservationEntry e)
                    {
                        if (!string.IsNullOrWhiteSpace(e.Record.CorrelationId))
                        _locks.TryRemove((e.Record.AtmId, e.Record.Stan), out _);
                    }
                });

                _cache.Set(resKey, updatedEntry, options);
            }
        }

        public bool TryGetReservationByStan(string atmId, long originalStan, out ReservationRecord record)
        {
            record = default!;
            if (_cache.TryGetValue(ResKey(atmId, originalStan), out ReservationEntry entry))
            {
                record = entry.Record;
                return true;
            }
            return false;
        }

        public bool TryGetCompletionResponse(string atmId, long stan, out byte[] a73Bytes)
        {
            a73Bytes = default!;       
            if (_cache.TryGetValue(A73Key(atmId, stan), out byte[] b))
            {
                a73Bytes = Clone(b);
                return true;
            }
            return false;
        }

        public void StoreCompletionResponse(string atmId, long stan, byte[] a73Bytes)
        {
            _cache.Set(
                A73Key(atmId, stan)
                , Clone(a73Bytes)
                , new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = _responseTtl });
        }

        private static byte[] Clone(byte[] bytes) => (byte[])bytes.Clone();
    }
}