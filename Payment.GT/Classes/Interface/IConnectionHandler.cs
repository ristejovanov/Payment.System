using System;
using System.Threading;
using System.Threading.Tasks;

namespace Payment.GT.Interfaces
{
    public interface IConnectionHandler
    {
        Task RunAsync(CancellationToken ct);
    }
}