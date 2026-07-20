using MassTransit;
using MediatR;
using Microsoft.Extensions.Logging;
using PSP.TopupService.Application.Common.Context;
using PSP.TopupService.Application.Common.Inbox;
using PSP.TopupService.Application.Topups.Commands.PerformTopup;
using PSP.TopupService.Contracts.Events;

namespace PSP.TopupService.Infrastructure.Messaging.Consumers;

/// <summary>
/// Consumes <see cref="PaymentRequestedEvent"/> (published when a payment has
/// settled and the topup can now proceed). Dispatches the
/// <see cref="PerformTopupCommand"/> which performs the MCI topup and — on
/// terminal failure — the Bank reversal. Idempotency is enforced at the
/// consumer inbox gate and at the aggregate state check.
/// </summary>
public sealed class PaymentRequestedConsumer : IConsumer<PaymentRequestedEvent>
{
    private readonly IMediator _mediator;
    private readonly IInboxStore _inbox;
    private readonly ICorrelationContext _correlation;
    private readonly ILogger<PaymentRequestedConsumer> _logger;

    public PaymentRequestedConsumer(
        IMediator mediator,
        IInboxStore inbox,
        ICorrelationContext correlation,
        ILogger<PaymentRequestedConsumer> logger)
    {
        _mediator = mediator;
        _inbox = inbox;
        _correlation = correlation;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<PaymentRequestedEvent> context)
    {
        var message = context.Message;
        var messageId = context.MessageId ?? Guid.NewGuid();

        _correlation.Set(message.CorrelationId, messageId, context.CorrelationId?.ToString(), null, null);

        _logger.LogInformation(
            "شروع فراخوانی API همراه اول - تراکنش {TopupId} - شناسه همبستگی {CorrelationId}",
            message.TopupId,
            message.CorrelationId);

        var firstTime = await _inbox.TryMarkProcessingAsync(messageId, nameof(PaymentRequestedConsumer), null, context.CancellationToken);
        if (!firstTime)
        {
            _logger.LogWarning(
                "پیام تکراری PaymentRequested برای تراکنش {TopupId} نادیده گرفته شد",
                message.TopupId);
            return;
        }

        var command = new PerformTopupCommand
        {
            TopupId = message.TopupId,
            MessageId = messageId,
        };
        var result = await _mediator.Send(command, context.CancellationToken);

        if (result.WasIdempotentReplay)
        {
            _logger.LogInformation(
                "تراکنش {TopupId} از قبل پردازش شده بود (وضعیت: {Status})",
                message.TopupId,
                result.FinalStatus);
        }
        else
        {
            _logger.LogInformation(
                "پایان عملیات - تراکنش {TopupId} - وضعیت نهایی: {Status}",
                message.TopupId,
                result.FinalStatus);
        }
    }
}
