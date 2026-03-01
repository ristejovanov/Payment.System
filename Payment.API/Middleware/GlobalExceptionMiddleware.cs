public sealed class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _log;

    public ExceptionHandlingMiddleware(
        RequestDelegate next,
        ILogger<ExceptionHandlingMiddleware> log)
    {
        _next = next;
        _log = log;
    }


    // this can be made much much more sophisticated (e.g. handle different exception types differently, include stack traces in dev, etc.)
    public async Task Invoke(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            var errorId = context.TraceIdentifier;

            _log.LogError(ex, "Unhandled exception occurred. errorId={ErrorId}", errorId);
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            context.Response.ContentType = "application/json";

            var response = new
            {
                rc = "96",
                message = "System malfunction",
                errorId
            };

            await context.Response.WriteAsJsonAsync(response);
        }
    }
}