using System.Diagnostics;
using PSP.TopupService.Application.Common.Context;

namespace PSP.TopupService.Api.Middleware;

/// <summary>
/// Emits a single structured log entry per request: method, path, status,
/// elapsed milliseconds and correlation id. Operates at the very edge so even
/// middleware-level failures are visible in the log.
/// </summary>
public sealed class RequestLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RequestLoggingMiddleware> _logger;

    public RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, ICorrelationContext correlation)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            await _next(context);
        }
        finally
        {
            stopwatch.Stop();
            _logger.LogInformation(
                "درخواست {Method} {Path} -> {StatusCode} در {ElapsedMilliseconds}ms - شناسه همبستگی {CorrelationId}",
                context.Request.Method,
                context.Request.Path,
                context.Response.StatusCode,
                stopwatch.ElapsedMilliseconds,
                correlation.CorrelationId);
        }
    }
}
