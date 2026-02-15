namespace Security.API.Middleware
{
    public class CorrelationIdMiddleware
    {
        private readonly RequestDelegate _next;
        private const string HeaderName = "X-Correlation-ID";

        public CorrelationIdMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            // Use existing correlation ID or generate a new one
            var correlationId = context.Request.Headers.ContainsKey(HeaderName)
                ? context.Request.Headers[HeaderName].ToString()
                : Guid.NewGuid().ToString();

            // Store for later use (e.g., logging and exception middleware)
            context.Items["CorrelationId"] = correlationId;

            // Add to response header for the client
            context.Response.Headers[HeaderName] = correlationId;

            await _next(context);
        }
    }
}
