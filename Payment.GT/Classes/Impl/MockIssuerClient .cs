using Payment.GT.Classes.Interface;
using Payment.Shared.Dto;
using System;
using System.Collections.Generic;
using System.Text;

namespace Payment.GT.Classes.Impl
{
    public sealed class MockIssuerClient : IIssuerClient
    {
        public async Task<IssuerDecision> AuthorizeAsync(string pan, string pinBlock, int amountMinor, string currency, CancellationToken ct)
        {
            // Example rules:
            // Approve exact pan 4111111111111111
            if (pan == "4111111111111111")
                return new IssuerDecision { Rc = "00", AuthCode = "831992", Message = "APPROVED" };

            // Decline insufficient funds
            if (pan.StartsWith("5"))
                return new IssuerDecision { Rc = "51", AuthCode = null, Message = "INSUFFICIENT_FUNDS" };

            // Simulate slow issuer for testing timeouts
            if (pan.EndsWith("0000"))
            {
                await Task.Delay(TimeSpan.FromSeconds(5), ct);
                return new IssuerDecision { Rc = "00", AuthCode = "123456", Message = "APPROVED_LATE" };
            }

            return new IssuerDecision { Rc = "05", AuthCode = null, Message = "DO_NOT_HONOR" };
        }
    }
}
