using MassTransit;
using MediatR;
using Microsoft.Extensions.Logging;
using PSP.TopupService.Application.Common.Context;
using PSP.TopupService.Application.Common.Inbox;
using PSP.TopupService.Application.Topups.Commands.ProcessPaymentResult;
using PSP.TopupService.Contracts.Events;

namespace PSP.TopupService.Infrastructure.Messaging.Consumers;

/// <summary>
/// Consumes <see cref="PaymentCompletedEvent"/> from the Bank. Implements the
/// consume-local idempotency pattern: the inbox insert and the business change
/// commit together, so a redelivered message becomes a no-op.
/// </summary>
public sealed class PaymentCompletedConsumer : IConsumer<PaymentCompletedEvent>
{
    private readonly IMediator _mediator;
    private readonly IInboxStore _inbox;
    private readonly ICorrelationContext _correlation;
    private readonly ILogger<PaymentCompletedConsumer> _logger;

    public PaymentCompletedConsumer(
        IMediator mediator,
        IInboxStore inbox,
        ICorrelationContext correlation,
        ILogger<PaymentCompletedConsumer> logger)
    {
        _mediator = mediator;
        _inbox = inbox;
        _correlation = correlation;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<PaymentCompletedEvent> context)
    {
        var message = context.Message;
        var messageId = context.MessageId ?? Guid.NewGuid();

        // Propagate correlation id into the ambient context so log scopes and
        // downstream outbox messages carry it.
        _correlation.Set(message.CorrelationId, messageId, context.CorrelationId?.ToString(), null, null);

        _logger.LogInformation(
            "دریافت نتیجه پرداخت برای تراکنش {TopupId} - موفق: {Succeeded} - شناسه همبستگی {CorrelationId}",
            message.TopupId,
            message.Succeeded,
            message.CorrelationId);

        // 1. Idempotency gate: if the inbox insert returns false, this message
        //    was already processed — acknowledge and stop.
        var firstTime = await _inbox.TryMarkProcessingAsync(messageId, nameof(PaymentCompletedConsumer), null, context.CancellationToken);
        if (!firstTime)
        {
            _logger.LogWarning(
                "پیام تکراری برای تراکنش {TopupId} نادیده گرفته شد - شناسه پیام {MessageId}",
                message.TopupId,
                messageId);
            return;
        }

        // 2. Dispatch the domain work to the application layer.
        var command = new ProcessPaymentResultCommand
        {
            TopupId = message.TopupId,
            MessageId = messageId,
            BankReference = message.BankReference,
            BankSource = "BANK",
            Succeeded = message.Succeeded,
            FailureReason = message.FailureReason,
            ConfirmedAtUtc = message.ConfirmedAtUtc,
        };

        var result = await _mediator.Send(command, context.CancellationToken);

        if (result.WasIdempotentReplay)
        {
            _logger.LogInformation(
                "نتیجه پرداخت برای تراکنش {TopupId} قبلاً اعمال شده بود (وضعیت: {Status})",
                message.TopupId,
                result.NewStatus);
        }
        else
        {
            _logger.LogInformation(
                "پرداخت {Outcome} برای تراکنش {TopupId} - وضعیت جدید: {Status}",
                message.Succeeded ? "موفق" : "ناموفق",
                message.TopupId,
                result.NewStatus);
        }
    }
}
