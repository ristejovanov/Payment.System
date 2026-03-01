namespace Payment.Shared.Enums
{
    /// <summary>
    /// Outcome of attempting to begin a reservation in the gateway state store
    /// </summary>
    public enum BeginReservationOutcome
    {
        /// <summary>
        /// New reservation started successfully (first time processing)
        /// </summary>
        StartedNew,

        /// <summary>
        /// Reservation is currently being processed (duplicate request arrived before completion)
        /// </summary>
        AlreadyProcessing,

        /// <summary>
        /// Reservation already completed - return cached A71 response
        /// </summary>
        AlreadyCompleted,

        /// <summary>
        /// STAN collision: same STAN but different payload (fingerprint mismatch)
        /// </summary>
        StanReuseDifferentPayload
    }
}