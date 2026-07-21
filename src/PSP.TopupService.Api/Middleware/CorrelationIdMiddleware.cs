using PSP.TopupService.Application.Common.Context;

namespace PSP.TopupService.Api.Middleware;

/// <summary>
/// Populates the <c>X-Correlation-Id</c> response header and the ambient
/// <see cref="ICorrelationContext"/> from the inbound request. The correlation
/// id is either the caller's header (when present) or a fresh Guid. Every
/// subsequent log entry, outbox message and broker message carries it.
/// </summary>
public sealed class CorrelationIdMiddleware
{
    public const string HeaderName = "X-Correlation-Id";

    private readonly RequestDelegate _next;

    public CorrelationIdMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, ICorrelationContext correlation)
    {
        var correlationId = GetOrNewCorrelationId(context);
        var requestId = Guid.NewGuid();

        correlation.Set(correlationId, requestId, context.TraceIdentifier, context.User?.Identity?.Name, context.Connection.RemoteIpAddress?.ToString());

        context.Response.Headers[HeaderName] = correlationId.ToString();
        context.Items[HeaderName] = correlationId;

        await _next(context);
    }

    private static Guid GetOrNewCorrelationId(HttpContext context)
    {
        if (context.Request.Headers.TryGetValue(HeaderName, out var header) && Guid.TryParse(header, out var existing))
        {
            return existing;
        }

        return Guid.NewGuid();
    }
}
