using Payment.Shared.Dto;
using System;
using System.Collections.Generic;
using System.Text;

namespace Payment.GT.Classes.Interface
{
    public interface IGatewayStateStore
    {
        bool TryGetResponse(string key, out byte[] responseFrame);
        void StoreResponse(string key, byte[] responseFrame);

        bool TryGetReservationByStan(string atmId, long stan, out ReservationRecord rec);
        bool TryGetReservationByCorrelation(string correlationId, out ReservationRecord rec);
        void UpsertReservation(ReservationRecord rec);
    }
}
