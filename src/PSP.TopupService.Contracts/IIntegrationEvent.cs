namespace PSP.TopupService.Contracts;

/// <summary>
/// Marker interface for every integration event exchanged on the message bus.
/// All events carry a correlation id so the end-to-end flow can be traced.
/// </summary>
public interface IIntegrationEvent
{
    /// <summary>Stable type discriminator (matches the outbox <c>$type</c> field).</summary>
    string EventType { get; }

    /// <summary>Correlation id propagated across the whole topup flow.</summary>
    Guid CorrelationId { get; }

    /// <summary>UTC instant the event was produced.</summary>
    DateTime OccurredOnUtc { get; }

    /// <summary>Schema version of the payload.</summary>
    int Version { get; }
}
