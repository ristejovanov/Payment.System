using Payment.Shared.Requests;
using Payment.Shared.Responses;

namespace ATM.DataServices.interfaces;

/// <summary>
/// Interface for handling withdrawal operations via GT Gateway
/// </summary>
public interface IWithdrawalService
{
    /// <summary>
    /// Reserve a withdrawal
    /// </summary>
    /// <param name="request">The reservation request details</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The reservation response containing correlation ID, STAN, and result</returns>
    Task<ReserveWithdrawalResponse> ReserveAsync(
        ReserveWithdrawalRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Complete a withdrawal
    /// </summary>
    /// <param name="request">The completion request details</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The completion response containing result status</returns>
    Task<CompleteWithdrawalResponse> CompleteAsync(
        CompleteWithdrawalRequest request,
        CancellationToken cancellationToken = default);
}