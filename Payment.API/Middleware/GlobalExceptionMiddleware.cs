using Security.Shared.Enums;
using Security.Shared;


namespace Security.API.Middleware
{
    public class GlobalExceptionMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<GlobalExceptionMiddleware> _logger;

        public GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext ctx)
        {
            try
            {
                await _next(ctx);
            }
            catch (Exception ex)
            {
                await HandleExceptionAsync(ctx, ex);
            }
        }

        private async Task HandleExceptionAsync(HttpContext ctx, Exception ex)
        {
            // Retrieve correlation ID for traceability
            var correlationId = ctx.Items.TryGetValue("CorrelationId", out var cid)
                ? cid?.ToString()
                : Guid.NewGuid().ToString();

            // Map known exceptions
            (EDataResponseCode code, int status, string msg, bool isKnown) = ex switch
            {
                ValidationException => (EDataResponseCode.InvalidInputParameter, 400, ex.Message, true),
                ConflictException => (EDataResponseCode.Conflict, 409, ex.Message, true),
                NotFoundException => (EDataResponseCode.NotFound, 404, ex.Message, true),
                RepositoryException => (EDataResponseCode.InternalError, 500, "Database error occurred.", true), //i would like to log because i would like to know what is wrong with the sql access 
                _ => (EDataResponseCode.InternalError, 500, "Unexpected error occurred.", false)
            };

            // Log differently for known vs unknown exceptions
            if (isKnown)
            {
                if (ex is RepositoryException repoEx)
                {
                    // Log full stack trace + message for RepositoryException
                    _logger.LogError(repoEx,
                        "Repository exception occurred while accessing database: {Message} (CorrelationId: {CID})",
                        repoEx.Message, correlationId);
                }
                else
                {
                    // Log short message for other known exceptions
                    _logger.LogWarning("Handled {ExceptionType}: {Message} (CorrelationId: {CID})",
                        ex.GetType().Name, ex.Message, correlationId);
                }
            }
            else
            {
                // Unknown exception: include full call stack
                _logger.LogError(ex, "Unhandled exception occurred (CorrelationId: {CID})", correlationId);
            }

            // Always send a safe, consistent response
            ctx.Response.StatusCode = status;
            ctx.Response.ContentType = "application/json";

            var response = new DataResponse<object>
            {
                ResponseCode = code,
                ErrorMessage = msg,
                CorrelationId = correlationId!
            };

            await ctx.Response.WriteAsJsonAsync(response);
        }
    }
}
