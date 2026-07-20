using System.Threading;

namespace PSP.TopupService.Application.Common.Context;

/// <summary>
/// Async-local implementation of <see cref="ICorrelationContext"/>. Each
/// logical async flow (request / consumer invocation) gets its own copy, so
/// background workers do not bleed identifiers across concurrent messages.
/// </summary>
public sealed class CorrelationContext : ICorrelationContext
{
    private static readonly AsyncLocal<CorrelationState?> Current = new();

    public Guid CorrelationId => Current.Value?.CorrelationId ?? Guid.Empty;
    public Guid RequestId => Current.Value?.RequestId ?? Guid.Empty;
    public string? TraceId => Current.Value?.TraceId;
    public string? Actor => Current.Value?.Actor;
    public string? RemoteIp => Current.Value?.RemoteIp;

    public void Set(Guid correlationId, Guid requestId, string? traceId, string? actor, string? remoteIp)
    {
        Current.Value = new CorrelationState(correlationId, requestId, traceId, actor, remoteIp);
    }

    /// <summary>Clears the context for the current async flow.</summary>
    public static void Clear() => Current.Value = null;

    private sealed class CorrelationState(Guid correlationId, Guid requestId, string? traceId, string? actor, string? remoteIp)
    {
        public Guid CorrelationId { get; } = correlationId;
        public Guid RequestId { get; } = requestId;
        public string? TraceId { get; } = traceId;
        public string? Actor { get; } = actor;
        public string? RemoteIp { get; } = remoteIp;
    }
}
