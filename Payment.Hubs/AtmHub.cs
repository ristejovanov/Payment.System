using Microsoft.AspNetCore.SignalR;
using System;
using System.Text.RegularExpressions;

namespace Payment.Hubs
{
    public sealed class AtmHub : Hub
    {
        public Task JoinAtm(string atmId) => Groups.AddToGroupAsync(Context.ConnectionId, $"atm:{atmId}");
        public Task JoinTxn(string correlationId) => Groups.AddToGroupAsync(Context.ConnectionId, $"txn:{correlationId}");
    }
}
