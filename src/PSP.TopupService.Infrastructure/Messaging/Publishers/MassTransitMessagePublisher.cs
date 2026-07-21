using MassTransit;
using Microsoft.Extensions.Logging;
using PSP.TopupService.Contracts;
using PSP.TopupService.Infrastructure.Messaging.Abstractions;

namespace PSP.TopupService.Infrastructure.Messaging.Publishers;

/// <summary>
/// MassTransit-backed <see cref="IMessagePublisher"/>. Wraps the bus
/// <c>IPublishEndpoint</c> so application code depends only on the abstraction.
/// The broker MessageId is set to the outbox row id so accidental re-publishes
/// collapse at the broker (or, failing that, at the consumer inbox).
/// </summary>
public sealed class MassTransitMessagePublisher : IMessagePublisher
{
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly ILogger<MassTransitMessagePublisher> _logger;

    public MassTransitMessagePublisher(IPublishEndpoint publishEndpoint, ILogger<MassTransitMessagePublisher> logger)
    {
        _publishEndpoint = publishEndpoint;
        _logger = logger;
    }

    public async Task PublishAsync<TEvent>(TEvent @event, Guid messageId, Guid correlationId, CancellationToken cancellationToken = default)
        where TEvent : class, IIntegrationEvent
    {
        ArgumentNullException.ThrowIfNull(@event);

        _logger.LogDebug(
            "ارسال پیام {EventType} با شناسه {MessageId} - شناسه همبستگی {CorrelationId}",
            @event.EventType,
            messageId,
            correlationId);

        await _publishEndpoint.Publish(
            @event,
            context =>
            {
                context.MessageId = messageId;
                context.CorrelationId = correlationId;
                context.Headers.Set("x-event-type", @event.EventType);
                context.Headers.Set("x-event-version", @event.Version.ToString(System.Globalization.CultureInfo.InvariantCulture));
                context.Headers.Set("x-occurred-on-utc", @event.OccurredOnUtc.ToString("O"));
            },
            cancellationToken);
    }
}
