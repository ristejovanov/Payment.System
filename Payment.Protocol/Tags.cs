using System;
using System.Collections.Generic;
using System.Text;

namespace Payment.Protocol
{
    public class Tags
    {
        // Common
        public const byte AtmId = 0x01;
        public const byte Stan = 0x02;
        public const byte LocalDateTime = 0x03;
        public const byte CorrelationId = 0x04;
        public const byte IsRepeat = 0x05;

        // Reservation
        public const byte Pan = 0x10;
        public const byte ExpiryYYMM = 0x11;
        public const byte PinBlock = 0x12;
        public const byte AmountMinor = 0x20;
        public const byte Currency = 0x21;

        // Completion
        public const byte DispenseResult = 0x30;
        public const byte DispensedAmountMinor = 0x31;
        public const byte OriginalStan = 0x32;
        public const byte CompletionStatus = 0x33;

        // Response
        public const byte Rc = 0x40; // ASCII "00"/"05"/"51"/"91"...
        public const byte AuthCode = 0x41; // ASCII 6
        public const byte Message = 0x42; // ASCII text
         
        // Bonus
        public const byte IccData = 0x60; // binary blob (optional)
    }
}
