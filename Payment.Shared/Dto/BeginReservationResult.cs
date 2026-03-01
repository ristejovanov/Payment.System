using Payment.Shared.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Payment.Shared.Dto
{
    public sealed record BeginReservationResult(
     BeginReservationOutcome Outcome,
     ReservationRecord? Record,
     byte[]? CachedA71Bytes,
     string? ErrorMessage);
}
