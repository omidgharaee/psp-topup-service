using PSP.TopupService.Contracts;

namespace PSP.TopupService.Infrastructure.Messaging.Abstractions;

/// <summary>
/// Publishes an integration event to the message broker. Implementations live
/// in Infrastructure (MassTransit-backed). The publisher sets the broker
/// MessageId to the supplied <c>messageId</c> argument so the broker's own
/// de-duplication can collapse accidental re-publishes from the outbox.
/// </summary>
public interface IMessagePublisher
{
    /// <summary>
    /// Publishes the event, tagging the broker message with the supplied
    /// message id and correlation id.
    /// </summary>
    Task PublishAsync<TEvent>(TEvent @event, Guid messageId, Guid correlationId, CancellationToken cancellationToken = default)
        where TEvent : class, IIntegrationEvent;
}
