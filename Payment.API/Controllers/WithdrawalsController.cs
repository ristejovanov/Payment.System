using ATM.DataServices.interfaces;
using Microsoft.AspNetCore.Mvc;
using Payment.Shared.Requests;
using Payment.Shared.Responses;

namespace Payment.API.Controllers
{

    [ApiController]
    [Route("api/[controller]")]
    public class WithdrawalsController : ControllerBase
    {
        private readonly IWithdrawalService _withdrawalService;
        private readonly ILogger<WithdrawalsController> _logger;

        public WithdrawalsController(
            IWithdrawalService withdrawalService,
            ILogger<WithdrawalsController> logger)
        {
            _withdrawalService = withdrawalService;
            _logger = logger;
        }

        /// <summary>
        /// Reserve a withdrawal (Step 1)
        /// </summary>
        [HttpPost("reserve")]
        [ProducesResponseType(typeof(ReserveWithdrawalResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ReserveWithdrawalResponse>> Reserve([FromBody] ReserveWithdrawalRequest request,
            CancellationToken cancellationToken)
        {
                _logger.LogInformation("Reserve withdrawal request received for ATM {AtmId}", request.AtmId);

                var response = await _withdrawalService.ReserveAsync(request, cancellationToken);
                return Ok(response);
        }

        /// <summary>
        /// Completes a previously approved withdrawal by reporting ATM dispense result.
        /// </summary>
        [HttpPost("complete")]
        [ProducesResponseType(typeof(CompleteWithdrawalResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
        public async Task<ActionResult<CompleteWithdrawalResponse>> Complete(
            [FromBody] CompleteWithdrawalRequest request,
            CancellationToken ct)
        {
            _logger.LogInformation("Complete withdrawal request received for ATM {AtmId}", request.AtmId);

            var result = await _withdrawalService.CompleteAsync(request, ct);
            return Ok(result);
        }
    }
}
