namespace PSP.TopupService.Application.Common.Context;

/// <summary>
/// Ambient context for tracing identifiers. Populated by middleware
/// (CorrelationId, RequestId) and consumed by command handlers, log enrichers
/// and outbox messages so that a single business operation carries the same
/// correlation id end-to-end.
/// </summary>
public interface ICorrelationContext
{
    /// <summary>Correlation id propagated across the whole topup flow (API → broker → consumers).</summary>
    Guid CorrelationId { get; }

    /// <summary>Unique id of the current request; differs per inbound message/HTTP call.</summary>
    Guid RequestId { get; }

    /// <summary>OpenTelemetry/W3C trace id, when available.</summary>
    string? TraceId { get; }

    /// <summary>Id of the authenticated principal initiating the operation, if any.</summary>
    string? Actor { get; }

    /// <summary>Remote IP of the caller, if known.</summary>
    string? RemoteIp { get; }

    /// <summary>Resets the context for a new operation (called by middleware on each request).</summary>
    void Set(Guid correlationId, Guid requestId, string? traceId, string? actor, string? remoteIp);
}
