using MassTransit;
using MediatR;
using Microsoft.Extensions.Logging;
using PSP.TopupService.Application.Common.Context;
using PSP.TopupService.Application.Common.Inbox;
using PSP.TopupService.Application.Topups.Commands.PerformAdvice;
using PSP.TopupService.Contracts.Events;

namespace PSP.TopupService.Infrastructure.Messaging.Consumers;

/// <summary>
/// Consumes <see cref="AdviceRequestedEvent"/> and dispatches the
/// <see cref="PerformAdviceCommand"/>. Inbox idempotency is keyed per message
/// id (each retry carries a fresh outbox row -> fresh message id), so the
/// consume-local gate only catches genuine broker redeliveries, not the
/// scheduled retries themselves.
/// </summary>
public sealed class AdviceRequestedConsumer : IConsumer<AdviceRequestedEvent>
{
    private readonly IMediator _mediator;
    private readonly IInboxStore _inbox;
    private readonly ICorrelationContext _correlation;
    private readonly ILogger<AdviceRequestedConsumer> _logger;

    public AdviceRequestedConsumer(
        IMediator mediator,
        IInboxStore inbox,
        ICorrelationContext correlation,
        ILogger<AdviceRequestedConsumer> logger)
    {
        _mediator = mediator;
        _inbox = inbox;
        _correlation = correlation;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<AdviceRequestedEvent> context)
    {
        var message = context.Message;
        var messageId = context.MessageId ?? Guid.NewGuid();

        _correlation.Set(message.CorrelationId, messageId, context.CorrelationId?.ToString(), null, null);

        _logger.LogInformation(
            "دریافت درخواست Advice برای تراکنش {TopupId} (تلاش شماره {Attempt})",
            message.TopupId,
            message.AdviceAttempt + 1);

        var firstTime = await _inbox.TryMarkProcessingAsync(messageId, nameof(AdviceRequestedConsumer), null, context.CancellationToken);
        if (!firstTime)
        {
            _logger.LogWarning("پیام تکراری Advice برای تراکنش {TopupId} نادیده گرفته شد", message.TopupId);
            return;
        }

        var command = new PerformAdviceCommand
        {
            TopupId = message.TopupId,
            MessageId = messageId,
            AdviceAttempt = message.AdviceAttempt,
        };

        var result = await _mediator.Send(command, context.CancellationToken);

        _logger.LogInformation(
            "نتیجه Advice برای تراکنش {TopupId}: {Status}{Replay}",
            message.TopupId,
            result.FinalStatus,
            result.WasIdempotentReplay ? " (replay)" : string.Empty);
    }
}
