using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace Payment.GT.Classes.Impl
{
    public static class RequestFingerprint
    {
        // Hash only the fields that must be identical for the same (atmId, stan)
        public static string ForA70(string pan, string expiryYYMM, string pinBlock, int amountMinor, string currency)
        {
            var s = $"{pan}|{expiryYYMM}|{pinBlock}|{amountMinor}|{currency}";
            return Sha256Hex(s);
        }

        private static string Sha256Hex(string input)
        {
            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
            // hex uppercase
            var sb = new StringBuilder(bytes.Length * 2);
            foreach (var b in bytes) sb.Append(b.ToString("X2"));
            return sb.ToString();
        }
    }
}
