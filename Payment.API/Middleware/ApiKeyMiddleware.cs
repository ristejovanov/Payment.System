using Security.DataServices.interfaces;
using Security.Shared;
using Security.Shared.Enums;

namespace Security.API.Middleware
{

    namespace Web.Middleware
    {
        public class ApiKeyMiddleware
        {
            private readonly RequestDelegate _next;
            private readonly ILogger<ApiKeyMiddleware> _logger;

            public ApiKeyMiddleware(RequestDelegate next, ILogger<ApiKeyMiddleware> logger)
            {
                _next = next;
                _logger = logger;
            }

            public async Task InvokeAsync(HttpContext context, IClientService clientService)
            {

                // Require header
                if (!context.Request.Headers.TryGetValue("X-API-KEY", out var extractedKey))
                {
                    await WriteResponse(context, EDataResponseCode.Unauthorized, "Missing API key");
                    return;
                }

                // Validate key
                var client = await clientService.Validate(extractedKey!);
                if (client == null)
                {
                    await WriteResponse(context, EDataResponseCode.Unauthorized, "Invalid API key");
                    return;
                }

                if (!client.IsActive)
                {
                    await WriteResponse(context, EDataResponseCode.Unauthorized, "Client inactive");
                    return;
                }

                // Attach to context for logging
                context.Items["ClientId"] = client.ClientId;
                context.Items["ClientName"] = client.ClientName;

                await _next(context);
            }

            private static async Task WriteResponse(HttpContext ctx, EDataResponseCode code, string message)
            {
                ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
                ctx.Response.ContentType = "application/json";
                var correlationId = ctx.Items["CorrelationId"]?.ToString();

                var response = new DataResponse<object>
                {
                    ResponseCode = code,
                    ErrorMessage = message,
                    CorrelationId = correlationId!
                };
                await ctx.Response.WriteAsJsonAsync(response);
            }
        }
    }
}
