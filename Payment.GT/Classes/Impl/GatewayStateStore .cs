using Payment.GT.Classes.Interface;
using Payment.Shared.Dto;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;

namespace Payment.GT.Classes.Impl
{
    internal class GatewayStateStore : IGatewayStateStore
    {
        private readonly ConcurrentDictionary<string, byte[]> _responses = new();
        private readonly ConcurrentDictionary<(string atmId, long stan), ReservationRecord> _resByStan = new();
        private readonly ConcurrentDictionary<string, ReservationRecord> _resByCorr = new();

        public bool TryGetResponse(string key, out byte[] responseFrame)
            => _responses.TryGetValue(key, out responseFrame!);

        public void StoreResponse(string key, byte[] responseFrame)
            => _responses[key] = responseFrame;

        public bool TryGetReservationByStan(string atmId, long stan, out ReservationRecord rec)
            => _resByStan.TryGetValue((atmId, stan), out rec!);

        public bool TryGetReservationByCorrelation(string correlationId, out ReservationRecord rec)
            => _resByCorr.TryGetValue(correlationId, out rec!);

        public void UpsertReservation(ReservationRecord rec)
        {
            _resByStan[(rec.AtmId, rec.Stan)] = rec;
            _resByCorr[rec.CorrelationId] = rec;
        }
    }
}
