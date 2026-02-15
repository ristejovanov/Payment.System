using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace Payment.Protocol
{
    public static class ToyPinBlock
    {
        public static string Compute(string pan, string pin, string correlationId)
        {
            var input = $"{pan}:{pin}:{correlationId}";
            var hash = SHA256.HashData(Encoding.ASCII.GetBytes(input));
            return Convert.ToHexString(hash)[..16]; 
        }
    }
}
