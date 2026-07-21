using MassTransit;
using Microsoft.Extensions.Options;
using PSP.Mock.Payment.Api.Options;
using PSP.TopupService.Contracts.Events;

namespace PSP.Mock.Payment.Api.Consumers;

/// <summary>
/// Consumes <c>AdviceRequestedEvent</c> from the Topup service. Simulates the
/// Payment application finalising (advising) a payment: after a configurable
/// delay it either publishes <c>AdviceCompletedEvent</c> (with a fresh advice
/// reference) or fails per the configured rate.
/// </summary>
public sealed class AdviceRequestedConsumer : IConsumer<AdviceRequestedEvent>
{
    private readonly IOptions<MockPaymentOptions> _options;
    private readonly ILogger<AdviceRequestedConsumer> _logger;

    public AdviceRequestedConsumer(IOptions<MockPaymentOptions> options, ILogger<AdviceRequestedConsumer> logger)
    {
        _options = options;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<AdviceRequestedEvent> context)
    {
        var message = context.Message;
        var opts = _options.Value;

        _logger.LogInformation(
            "درخواست Advice دریافت شد - تراکنش {TopupId} (تلاش {Attempt})",
            message.TopupId,
            message.AdviceAttempt + 1);

        if (opts.AdviceCompletionDelaySeconds > 0)
        {
            await Task.Delay(TimeSpan.FromSeconds(opts.AdviceCompletionDelaySeconds), context.CancellationToken);
        }

        if (ShouldFail(opts.AdviceFailureRate))
        {
            // No response published — the Topup service treats silence as a
            // failure and re-requests the advice via the outbox saga.
            _logger.LogWarning("Advice شبیه‌سازی‌شده شکست خورد - تراکنش {TopupId}", message.TopupId);
            return;
        }

        var reference = Guid.NewGuid().ToString("N");
        var ev = new AdviceCompletedEvent(
            message.TopupId,
            reference,
            "ADVICE",
            message.CorrelationId,
            DateTime.UtcNow);
        await context.Publish(ev, context.CancellationToken);

        _logger.LogInformation(
            "Advice موفق ارسال شد - تراکنش {TopupId} مرجع {Reference}",
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
