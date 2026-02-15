using Payment.Shared.Dto;
using System;
using System.Collections.Generic;
using System.Text;

namespace Payment.GT.Classes.Interface
{
    public interface IIssuerClient
    {
        Task<IssuerDecision> AuthorizeAsync(string pan, string pinBlock, int amountMinor, string currency, CancellationToken ct);
    }
}
