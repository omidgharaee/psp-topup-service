using MassTransit;
using Microsoft.Extensions.Options;
using PSP.Mock.Payment.Api.Options;
using PSP.TopupService.Contracts.Events;

namespace PSP.Mock.Payment.Api.Consumers;

/// <summary>
/// Consumes <c>PaymentRequestedEvent</c> from the Topup service. Simulates the
/// Payment application processing a payment: after a configurable delay and a
/// configurable failure rate it publishes <c>PaymentCompletedEvent</c> back.
/// </summary>
public sealed class PaymentRequestedConsumer : IConsumer<PaymentRequestedEvent>
{
    private readonly IOptions<MockPaymentOptions> _options;
    private readonly ILogger<PaymentRequestedConsumer> _logger;

    public PaymentRequestedConsumer(IOptions<MockPaymentOptions> options, ILogger<PaymentRequestedConsumer> logger)
    {
        _options = options;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<PaymentRequestedEvent> context)
    {
        var message = context.Message;
        var opts = _options.Value;

        _logger.LogInformation(
            "پرداخت شبیه‌سازی‌شده دریافت شد - تراکنش {TopupId} مبلغ {Amount}",
            message.TopupId,
            message.Amount);

        if (opts.PaymentCompletionDelaySeconds > 0)
        {
            await Task.Delay(TimeSpan.FromSeconds(opts.PaymentCompletionDelaySeconds), context.CancellationToken);
        }

        var succeeded = !ShouldFail(opts.PaymentFailureRate);

        var ev = new PaymentCompletedEvent(
            message.TopupId,
            message.BankReference,
            succeeded,
            succeeded ? null : "Simulated payment decline",
            DateTime.UtcNow,
            message.CorrelationId,
            DateTime.UtcNow);
        await context.Publish(ev, context.CancellationToken);

        _logger.LogInformation(
            "پاسخ پرداخت ارسال شد - تراکنش {TopupId} موفق: {Succeeded}",
            message.TopupId,
            succeeded);
    }

    private static bool ShouldFail(double rate)
    {
        if (rate <= 0)
        {
            return false;
        }

        if (rate >= 1)
        {
            return true;
        }

        return Random.Shared.NextDouble() < rate;
    }
}
