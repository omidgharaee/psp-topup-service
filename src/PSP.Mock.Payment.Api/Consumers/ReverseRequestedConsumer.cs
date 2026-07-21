using MassTransit;
using Microsoft.Extensions.Options;
using PSP.Mock.Payment.Api.Options;
using PSP.TopupService.Contracts.Events;

namespace PSP.Mock.Payment.Api.Consumers;

/// <summary>
/// Consumes <c>ReverseRequestedEvent</c> from the Topup service. Simulates the
/// Payment application reversing (refunding) a settled payment: after a
/// configurable delay it publishes <c>PaymentReversedEvent</c> with a fresh
/// reversal reference (or stays silent per the configured failure rate).
/// </summary>
public sealed class ReverseRequestedConsumer : IConsumer<ReverseRequestedEvent>
{
    private readonly IOptions<MockPaymentOptions> _options;
    private readonly ILogger<ReverseRequestedConsumer> _logger;

    public ReverseRequestedConsumer(IOptions<MockPaymentOptions> options, ILogger<ReverseRequestedConsumer> logger)
    {
        _options = options;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<ReverseRequestedEvent> context)
    {
        var message = context.Message;
        var opts = _options.Value;

        _logger.LogInformation(
            "درخواست برگشت وجه دریافت شد - تراکنش {TopupId} دلیل {Reason}",
            message.TopupId,
            message.Reason);

        if (opts.ReverseCompletionDelaySeconds > 0)
        {
            await Task.Delay(TimeSpan.FromSeconds(opts.ReverseCompletionDelaySeconds), context.CancellationToken);
        }

        if (ShouldFail(opts.ReverseFailureRate))
        {
            _logger.LogWarning("برگشت شبیه‌سازی‌شده شکست خورد - تراکنش {TopupId}", message.TopupId);
            return;
        }

        var reference = Guid.NewGuid().ToString("N");
        var ev = new PaymentReversedEvent(
            message.TopupId,
            reference,
            "REVERSAL",
            message.Reason,
            message.CorrelationId,
            DateTime.UtcNow);
        await context.Publish(ev, context.CancellationToken);

        _logger.LogInformation(
            "برگشت وجه موفق ارسال شد - تراکنش {TopupId} مرجع {Reference}",
            message.TopupId,
            reference);
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
