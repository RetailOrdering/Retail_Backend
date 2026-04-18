using System.Collections.Concurrent;
using System.Net;

namespace RetailOrdering.Middleware;

public class RateLimitingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RateLimitingMiddleware> _logger;
    private readonly int _requestLimit;
    private readonly TimeSpan _window;

    // Thread-safe dictionary: IP -> (request count, window start time)
    private static readonly ConcurrentDictionary<string, (int Count, DateTime WindowStart)> _clients = new();

    public RateLimitingMiddleware(RequestDelegate next, ILogger<RateLimitingMiddleware> logger, IConfiguration config)
    {
        _next = next;
        _logger = logger;
        _requestLimit = config.GetValue<int>("RateLimiting:RequestLimit", 100);
        _window = TimeSpan.FromSeconds(config.GetValue<int>("RateLimiting:WindowSeconds", 60));
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var ip = GetClientIp(context);

        if (!IsAllowed(ip))
        {
            _logger.LogWarning("Rate limit exceeded for IP: {IP}", ip);
            context.Response.StatusCode = (int)HttpStatusCode.TooManyRequests;
            context.Response.Headers["Retry-After"] = _window.TotalSeconds.ToString("F0");
            context.Response.ContentType = "application/json";

            await context.Response.WriteAsync("""
                {
                  "statusCode": 429,
                  "message": "Too many requests. Please slow down and try again later."
                }
                """);
            return;
        }

        await _next(context);
    }

    private bool IsAllowed(string ip)
    {
        var now = DateTime.UtcNow;

        _clients.AddOrUpdate(
            ip,
            // New entry
            _ => (1, now),
            // Update existing
            (_, existing) =>
            {
                if (now - existing.WindowStart > _window)
                    return (1, now); // Reset window
                return (existing.Count + 1, existing.WindowStart);
            }
        );

        var entry = _clients[ip];
        return entry.Count <= _requestLimit;
    }

    private static string GetClientIp(HttpContext context)
    {
        // Respect X-Forwarded-For for reverse proxies (e.g., nginx)
        var forwarded = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrEmpty(forwarded))
            return forwarded.Split(',')[0].Trim();

        return context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }
}

public static class RateLimitingMiddlewareExtensions
{
    public static IApplicationBuilder UseRateLimiting(this IApplicationBuilder app)
        => app.UseMiddleware<RateLimitingMiddleware>();
}