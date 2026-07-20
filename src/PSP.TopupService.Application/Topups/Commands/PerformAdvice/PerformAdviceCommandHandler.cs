using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PSP.TopupService.Application.Common.Abstractions;
using PSP.TopupService.Application.Common.Outbox;
using PSP.TopupService.Application.Topups.Abstractions;
using PSP.TopupService.Domain.Topups;
using PSP.TopupService.Domain.Topups.Enums;
using PSP.TopupService.Domain.Topups.ValueObjects;

namespace PSP.TopupService.Application.Topups.Commands.PerformAdvice;

/// <summary>
/// Handles <see cref="PerformAdviceCommand"/>: calls the Bank Advice endpoint
/// and either completes the transaction or schedules a retry. The transaction
/// NEVER auto-reverses on advice failure — the customer has been topped up, so
/// a reversal would steal the credit. Terminal advice failure flags the
/// transaction for manual reconciliation.
/// </summary>
public sealed class PerformAdviceCommandHandler : IRequestHandler<PerformAdviceCommand, PerformAdviceResult>
{
    private readonly ITopupRepository _repository;
    private readonly IBankAdviceClient _bank;
    private readonly IOutboxWriter _outbox;
    private readonly AdviceOptions _options;
    private readonly ILogger<PerformAdviceCommandHandler> _logger;

    public PerformAdviceCommandHandler(
        ITopupRepository repository,
        IBankAdviceClient bank,
        IOutboxWriter outbox,
        IOptions<AdviceOptions> options,
        ILogger<PerformAdviceCommandHandler> logger)
    {
        _repository = repository;
        _bank = bank;
        _outbox = outbox;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<PerformAdviceResult> Handle(PerformAdviceCommand request, CancellationToken cancellationToken)
    {
        var topup = await _repository.GetByIdForUpdateAsync(request.TopupId, cancellationToken)
            ?? throw new SharedKernel.Exceptions.NotFoundException(nameof(TopupTransaction), request.TopupId);

        // Idempotency: if the aggregate has already moved past AdvicePending
        // (Completed or AdviceFailed), this message is a replay.
        if (topup.Status != TopupStatus.AdvicePending)
        {
            return PerformAdviceResult.Replay(topup.Id, topup.Status.ToString());
        }

        if (topup.BankReference is null)
        {
            _logger.LogError("امکان Advice وجود ندارد: BankReference نال است - تراکنش {TopupId}", topup.Id);
            return PerformAdviceResult.Replay(topup.Id, topup.Status.ToString());
        }

        try
        {
            var advice = await _bank.AdviceAsync(topup.Id, topup.BankReference, topup.Amount, topup.CorrelationId, cancellationToken);

            var completed = topup.MarkAdviceCompleted(advice.AdviceReference, advice.CompletedAtUtc);
            if (completed.IsFailure)
            {
                return PerformAdviceResult.Replay(topup.Id, topup.Status.ToString());
            }

            // The topup is now genuinely terminal-successful.
            await _outbox.EnqueueTopupCompletedAsync(topup.Id, topup.MciReference!, topup.CorrelationId, cancellationToken);

            _logger.LogInformation("تأیید بانک (Advice) موفق بود - تراکنش {TopupId} تکمیل شد", topup.Id);
            return PerformAdviceResult.Done(topup.Id, topup.Status.ToString());
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "تلاش Advice بانک شکست خورد (شماره {Attempt}) - تراکنش {TopupId}", request.AdviceAttempt + 1, topup.Id);

            var failResult = topup.MarkAdviceAttemptFailed(ex.Message, _options.MaxRetries);
            if (failResult.IsFailure)
            {
                return PerformAdviceResult.Replay(topup.Id, topup.Status.ToString());
            }

            if (topup.FailureReason == TopupFailureReason.AdviceFailed)
            {
                // Terminal advice failure — DO NOT reverse (the topup succeeded).
                // Surface for manual reconciliation.
                _logger.LogError("Advice پس از {Attempts} تلاش شکست خورد - تراکنش {TopupId} نیاز به بررسی دستی دارد", _options.MaxRetries, topup.Id);
                return PerformAdviceResult.Done(topup.Id, topup.Status.ToString());
            }

            // Schedule the next advice attempt via the outbox with exponential back-off.
            var nextAttempt = request.AdviceAttempt + 1;
            var delay = TimeSpan.FromTicks((long)(_options.RetryDelay.Ticks * Math.Pow(_options.BackoffMultiplier, request.AdviceAttempt)));
            var processAfter = DateTime.UtcNow.Add(delay);

            await _outbox.EnqueueAdviceRequestedAsync(
                topup.Id,
                topup.BankReference,
                topup.Amount,
                topup.CorrelationId,
                processAfterUtc: processAfter,
                adviceAttempt: nextAttempt,
                cancellationToken);

            _logger.LogInformation("زمان‌بندی تلاش مجدد Advice برای تراکنش {TopupId} پس از {DelaySeconds}s", topup.Id, delay.TotalSeconds);
            return PerformAdviceResult.Done(topup.Id, topup.Status.ToString());
        }
    }
}
