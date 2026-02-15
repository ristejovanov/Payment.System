using Payment.Shared.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Payment.Shared.Dto
{

    public sealed record ReservationRecord(
        string AtmId,
        long Stan,
        string CorrelationId,
        ReservationStatus Status,
        string Rc,
        string? AuthCode,
        string? Message,
        DateTimeOffset CreatedUtc
    );
}
