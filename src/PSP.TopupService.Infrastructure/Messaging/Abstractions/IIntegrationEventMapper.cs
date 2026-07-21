using PSP.TopupService.Contracts;

namespace PSP.TopupService.Infrastructure.Messaging.Abstractions;

/// <summary>
/// Maps an outbox row's <c>$type</c> discriminator and JSON payload back to a
/// strongly-typed <see cref="IIntegrationEvent"/> instance so the publisher can
/// hand MassTransit a real object. Keeping this in Infrastructure means the
/// mapping rules live with the broker concerns, not in the Worker.
/// </summary>
public interface IIntegrationEventMapper
{
    /// <summary>Returns null when the type is unknown (handled as a failure).</summary>
    IIntegrationEvent? Map(string eventType, string payload);
}
