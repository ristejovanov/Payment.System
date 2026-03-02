
namespace Payment.GT.Classes.Interface
{
    /// <summary>
    /// Defines a contract for handling connection operations asynchronously.
    /// </summary>
    public interface IConnectionHandler
    {
        /// <summary>
        /// Runs the connection handler asynchronously.
        /// </summary>
        /// <param name="ct">The cancellation token to observe for cancellation requests.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        Task RunAsync(CancellationToken ct);
    }
}