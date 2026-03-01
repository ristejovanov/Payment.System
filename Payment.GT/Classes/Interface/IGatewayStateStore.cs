using Payment.Protocol.Dto;
using Payment.Shared.Dto;
using System;
using System.Collections.Generic;
using System.Text;

namespace Payment.GT.Classes.Interface
{
    public interface IGatewayStateStore
    {
        // --- Reservation lifecycle (A70/A71) ---

        /// <summary>
        /// Atomically creates a reservation in Processing state if it does not exist.
        /// If it exists, returns its current state and (if completed) cached response bytes.
        /// Also enforces payload fingerprint consistency for the same (atmId, stan).
        /// </summary>
        BeginReservationResult BeginReservation(A70RequestDto request, string fingerprint);

        /// <summary>
        /// Completes reservation state and stores the exact A71 bytes for deterministic replay.
        /// Must be safe to call multiple times (idempotent).
        /// </summary>
        void CompleteReservation(CompleteReservationRequest request);

        bool TryGetReservationByStan(string atmId, long stan, out ReservationRecord record);

        // --- Completion lifecycle (A72/A73) ---
        bool TryGetCompletionResponse(string atmId, long stan, out byte[] a73Bytes);
        void StoreCompletionResponse(string atmId, long stan, byte[] a73Bytes);

     }
}
