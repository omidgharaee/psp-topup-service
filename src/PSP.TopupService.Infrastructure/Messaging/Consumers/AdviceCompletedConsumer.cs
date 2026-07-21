using MassTransit;
using MediatR;
using Microsoft.Extensions.Logging;
using PSP.TopupService.Application.Common.Context;
using PSP.TopupService.Application.Common.Inbox;
using PSP.TopupService.Application.Topups.Commands.ApplyAdviceResult;
using PSP.TopupService.Contracts.Events;

namespace PSP.TopupService.Infrastructure.Messaging.Consumers;

/// <summary>
/// Consumes <see cref="AdviceCompletedEvent"/> from the Payment service. The
/// Payment service publishes this after successfully finalising (advising) a
/// payment. Inbox-gated; dispatches <see cref="ApplyAdviceResultCommand"/>.
/// </summary>
public sealed class AdviceCompletedConsumer : IConsumer<AdviceCompletedEvent>
{
    private readonly IMediator _mediator;
    private readonly IInboxStore _inbox;
    private readonly ICorrelationContext _correlation;
    private readonly ILogger<AdviceCompletedConsumer> _logger;

    public AdviceCompletedConsumer(
        IMediator mediator,
        IInboxStore inbox,
        ICorrelationContext correlation,
        ILogger<AdviceCompletedConsumer> logger)
    {
        _mediator = mediator;
        _inbox = inbox;
        _correlation = correlation;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<AdviceCompletedEvent> context)
    {
        var message = context.Message;
        var messageId = context.MessageId ?? Guid.NewGuid();

        _correlation.Set(message.CorrelationId, messageId, context.CorrelationId?.ToString(), null, null);

        _logger.LogInformation(
            "دریافت تأیید Advice برای تراکنش {TopupId} - مرجع {Reference}",
            message.TopupId,
            message.AdviceReference);

        var firstTime = await _inbox.TryMarkProcessingAsync(messageId, nameof(AdviceCompletedConsumer), null, context.CancellationToken);
        if (!firstTime)
        {
            _logger.LogWarning("پیام تکراری AdviceCompleted برای تراکنش {TopupId} نادیده گرفته شد", message.TopupId);
            return;
        }

        var command = new ApplyAdviceResultCommand
        {
            TopupId = message.TopupId,
            MessageId = messageId,
            AdviceReference = message.AdviceReference,
            AdviceSource = message.AdviceSource,
        };

        var result = await _mediator.Send(command, context.CancellationToken);

        _logger.LogInformation(
            "نتیجه Advice برای تراکنش {TopupId}: {Status}{Replay}",
            message.TopupId,
            result.FinalStatus,
            result.WasIdempotentReplay ? " (replay)" : string.Empty);
    }
}
