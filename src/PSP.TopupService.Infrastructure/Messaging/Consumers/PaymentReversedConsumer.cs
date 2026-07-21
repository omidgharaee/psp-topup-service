using MassTransit;
using MediatR;
using Microsoft.Extensions.Logging;
using PSP.TopupService.Application.Common.Context;
using PSP.TopupService.Application.Common.Inbox;
using PSP.TopupService.Application.Topups.Commands.ApplyReversalResult;
using PSP.TopupService.Contracts.Events;

namespace PSP.TopupService.Infrastructure.Messaging.Consumers;

/// <summary>
/// Consumes <see cref="PaymentReversedEvent"/> from the Payment service. The
/// Payment service publishes this in response to a <c>ReverseRequestedEvent</c>
/// after a topup failed terminally. Inbox-gated; dispatches
/// <see cref="ApplyReversalResultCommand"/>.
/// </summary>
public sealed class PaymentReversedConsumer : IConsumer<PaymentReversedEvent>
{
    private readonly IMediator _mediator;
    private readonly IInboxStore _inbox;
    private readonly ICorrelationContext _correlation;
    private readonly ILogger<PaymentReversedConsumer> _logger;

    public PaymentReversedConsumer(
        IMediator mediator,
        IInboxStore inbox,
        ICorrelationContext correlation,
        ILogger<PaymentReversedConsumer> logger)
    {
        _mediator = mediator;
        _inbox = inbox;
        _correlation = correlation;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<PaymentReversedEvent> context)
    {
        var message = context.Message;
        var messageId = context.MessageId ?? Guid.NewGuid();

        _correlation.Set(message.CorrelationId, messageId, context.CorrelationId?.ToString(), null, null);

        _logger.LogInformation(
            "دریافت نتیجه برگشت وجه برای تراکنش {TopupId} - مرجع {Reference}",
            message.TopupId,
            message.ReversalReference);

        var firstTime = await _inbox.TryMarkProcessingAsync(messageId, nameof(PaymentReversedConsumer), null, context.CancellationToken);
        if (!firstTime)
        {
            _logger.LogWarning("پیام تکراری PaymentReversed برای تراکنش {TopupId} نادیده گرفته شد", message.TopupId);
            return;
        }

        var command = new ApplyReversalResultCommand
        {
            TopupId = message.TopupId,
            MessageId = messageId,
            ReversalReference = message.ReversalReference,
            ReversalSource = message.ReversalSource,
        };

        var result = await _mediator.Send(command, context.CancellationToken);

        _logger.LogInformation(
            "نتیجه برگشت برای تراکنش {TopupId}: {Status}{Replay}",
            message.TopupId,
            result.FinalStatus,
            result.WasIdempotentReplay ? " (replay)" : string.Empty);
    }
}
