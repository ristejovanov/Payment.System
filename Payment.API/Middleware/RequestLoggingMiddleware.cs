using Serilog.Context;
using System.Text;
using System.Text.Json;

namespace Security.API.Middleware
{
    /// <summary>
    /// Middleware that logs incoming HTTP requests and responses,
    /// including client info, request parameters, and correlation ID.
    /// Sensitive fields are automatically masked.
    /// </summary>
    public class RequestLoggingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<RequestLoggingMiddleware> _logger;
        private const int MaxBodyLength = 2048;
        private static readonly JsonSerializerOptions JsonOpts = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };

        private readonly List<string> _sensitiveKeys ;

        public RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger, IConfiguration config)
        {
            _next = next;
            _logger = logger;
            _sensitiveKeys = config.GetSection("Logging:SensitiveKeys").Get<List<string>>()
                             ?? new List<string> { "password", "token" }; ;
        }

        public async Task InvokeAsync(HttpContext ctx)
        {
            // Allow re-reading the request body
            ctx.Request.EnableBuffering();

            var method = ctx.Request.Method;
            var path = ctx.Request.Path;
            var clientIp = ctx.Connection.RemoteIpAddress?.ToString();
            var clientName = ctx.Items["ClientName"]?.ToString() ?? "Unknown";
            var host = Environment.MachineName;
            var correlationId = ctx.Items["CorrelationId"]?.ToString();

            string parameters = await ReadRequestBodyAsync(ctx);

            using (LogContext.PushProperty("ClientIp", clientIp))
            using (LogContext.PushProperty("ClientName", clientName))
            using (LogContext.PushProperty("CorrelationId", correlationId))
            using (LogContext.PushProperty("RequestPath", path))
            using (LogContext.PushProperty("HttpMethod", method))
            {

                _logger.LogInformation(
                    "Request received | IP={ClientIp} | Client={ClientName} | Host={Host} | Method={Method} | Path={Path} | Params={Params} | CorrelationId={CorrelationId}",
                    clientIp, clientName, host, method, path, parameters, correlationId);
                await _next(ctx);
            }
        }

        private async Task<string> ReadRequestBodyAsync(HttpContext context)
        {
            try
            {
                if (context.Request.ContentLength is null || context.Request.ContentLength == 0)
                    return string.Empty;

                if (context.Request.ContentLength > MaxBodyLength)
                    return $"[Body too large: {context.Request.ContentLength} bytes]";

                using var reader = new StreamReader(context.Request.Body, Encoding.UTF8,
                    detectEncodingFromByteOrderMarks: false, leaveOpen: true);
                var body = await reader.ReadToEndAsync();
                context.Request.Body.Position = 0;

                if (string.IsNullOrWhiteSpace(body))
                    return string.Empty;

                var json = JsonSerializer.Deserialize<Dictionary<string, object>>(body, JsonOpts);
                if (json == null)
                    return body.Length > 500 ? body[..500] + "..." : body;

                MaskSensitive(json);
                return JsonSerializer.Serialize(json, JsonOpts);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to read or mask request body.");
                return "[Unparseable body]";
            }
        }

        private void MaskSensitive(Dictionary<string, object?> dict)
        {
            foreach (var key in dict.Keys.ToList())
            {
                if (_sensitiveKeys.Contains(key, StringComparer.OrdinalIgnoreCase))
                {
                    dict[key] = "****";
                    continue;
                }

                if (dict[key] is JsonElement el)
                {
                    switch (el.ValueKind)
                    {
                        case JsonValueKind.Object:
                            var nested =
                                JsonSerializer.Deserialize<Dictionary<string, object>>(el.GetRawText(), JsonOpts);
                            if (nested != null)
                            {
                                MaskSensitive(nested);
                                dict[key] = nested;
                            }

                            break;

                        case JsonValueKind.Array:
                            var arr = el.EnumerateArray().ToList();
                            for (int i = 0; i < arr.Count; i++)
                            {
                                if (arr[i].ValueKind == JsonValueKind.Object)
                                {
                                    var itemDict =
                                        JsonSerializer.Deserialize<Dictionary<string, object>>(arr[i].GetRawText(),
                                            JsonOpts);
                                    if (itemDict != null)
                                    {
                                        MaskSensitive(itemDict);
                                        arr[i] = JsonSerializer.SerializeToElement(itemDict);
                                    }
                                }
                            }

                            dict[key] = arr;
                            break;
                    }
                }
            }
        }
    }
}
