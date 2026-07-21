using MediatR;
using Microsoft.Extensions.Logging;
using PSP.TopupService.Application.Common.Abstractions;
using PSP.TopupService.Application.Common.Outbox;
using PSP.TopupService.Application.Topups.Abstractions;
using PSP.TopupService.Application.Topups.Clients;
using PSP.TopupService.Domain.Topups;
using PSP.TopupService.Domain.Topups.Enums;

namespace PSP.TopupService.Application.Topups.Commands.PerformTopup;

/// <summary>
/// Handles <see cref="PerformTopupCommand"/>: calls the mobile operator (with
/// Polly-backed resilience inside the client) and dispatches the next step via
/// the outbox. The Topup service NEVER talks to the Payment application over
/// HTTP — every Payment interaction (initial payment, advice, reversal) flows
/// through RabbitMQ.
///
/// - MCI success -> aggregate to AdvicePending + enqueue AdviceRequestedEvent.
/// - MCI terminal failure -> MarkTopupFailed + InitiateReversal + enqueue
///   ReverseRequestedEvent. The aggregate stays in TopupInProgress awaiting
///   the asynchronous PaymentReversedEvent that completes the reversal.
/// </summary>
public sealed class PerformTopupCommandHandler : IRequestHandler<PerformTopupCommand, PerformTopupResult>
{
    private readonly ITopupRepository _repository;
    private readonly IHamrahAvalClient _mci;
    private readonly IOutboxWriter _outbox;
    private readonly ILogger<PerformTopupCommandHandler> _logger;

    public PerformTopupCommandHandler(
        ITopupRepository repository,
        IHamrahAvalClient mci,
        IOutboxWriter outbox,
        ILogger<PerformTopupCommandHandler> logger)
    {
        _repository = repository;
        _mci = mci;
        _outbox = outbox;
        _logger = logger;
    }

    public async Task<PerformTopupResult> Handle(PerformTopupCommand request, CancellationToken cancellationToken)
    {
        var topup = await _repository.GetByIdForUpdateAsync(request.TopupId, cancellationToken)
            ?? throw new SharedKernel.Exceptions.NotFoundException(nameof(TopupTransaction), request.TopupId);

        // Idempotency: if the aggregate has already moved past TopupInProgress
        // (completed, failed or reversed), this message is a replay.
        if (topup.Status != TopupStatus.PaymentCompleted && topup.Status != TopupStatus.TopupInProgress)
        {
            return PerformTopupResult.Replay(topup.Id, topup.Status.ToString());
        }

        // 1. Start a fresh topup attempt on the aggregate.
        var attemptResult = topup.StartTopupAttempt();
        if (attemptResult.IsFailure)
        {
            return PerformTopupResult.Replay(topup.Id, topup.Status.ToString());
        }

        var attempt = attemptResult.Value!;

        // 2. Call the mobile operator. The client owns the Polly pipeline.
        HamrahAvalTopupResult? mciResult = null;
        try
        {
            mciResult = await _mci.TopupAsync(topup.Id, topup.MobileNumber, topup.Amount, topup.CorrelationId, cancellationToken);
        }
        catch (HamrahAvalException ex)
        {
            _logger.LogWarning(ex, "شارژ همراه اول شکست خورد - تراکنش {TopupId}", topup.Id);
        }

        // 3. Branch on outcome.
        if (mciResult is not null)
        {
            attempt.MarkSucceeded(mciResult.ProviderReference.Value);

            // MCI success moves the aggregate to AdvicePending — the topup is
            // done but the transaction is NOT terminal until the Payment service
            // confirms the advice. We enqueue AdviceRequestedEvent (via outbox)
            // and the Payment app responds asynchronously with AdviceCompletedEvent.
            var success = topup.MarkTopupSucceeded(mciResult.ProviderReference, mciResult.CompletedAtUtc);
            if (success.IsFailure)
            {
                _logger.LogError("ناسازگاری: ثبت موفقیت شارژ رد شد - تراکنش {TopupId}", topup.Id);
                return PerformTopupResult.Replay(topup.Id, topup.Status.ToString());
            }

            await _outbox.EnqueueAdviceRequestedAsync(
                topup.Id, topup.BankReference!, topup.Amount, topup.CorrelationId, processAfterUtc: null, adviceAttempt: 0, cancellationToken);

            _logger.LogInformation(
                "شارژ انجام شد - تراکنش {TopupId} - در انتظار تأیید سرویس Payment (Advice)",
                topup.Id);

            return PerformTopupResult.Done(topup.Id, topup.Status.ToString());
        }

        // ---- Topup failed terminally: request a reversal from the Payment service. ----
        attempt.MarkFailed("Polly exhausted or business rejection");

        var failReason = ClassifyTopupFailure();
        var failureMessage = "تمام تلاش‌های شارژ شکست خورد";
        var failResult = topup.MarkTopupFailed(failReason, failureMessage);
        if (failResult.IsFailure)
        {
            return PerformTopupResult.Replay(topup.Id, topup.Status.ToString());
        }

        await RequestReversalAsync(topup, cancellationToken);

        return PerformTopupResult.Done(topup.Id, topup.Status.ToString());
    }

    /// <summary>
    /// Records the reversal intent on the aggregate and enqueues a
    /// ReverseRequestedEvent. The aggregate stays in TopupInProgress with a
    /// ReverseRecord in Initiated state until the asynchronous
    /// PaymentReversedEvent arrives and the PaymentReversedConsumer completes
    /// the reversal. If the Payment service is unavailable the row stays in
    /// Initiated for manual reconciliation (it is NOT auto-reversed).
    /// </summary>
    private async Task RequestReversalAsync(TopupTransaction topup, CancellationToken cancellationToken)
    {
        if (topup.BankReference is null)
        {
            _logger.LogError("امکان برگشت وجود ندارد: مرجع پرداخت نال است - تراکنش {TopupId}", topup.Id);
            return;
        }

        var reversal = topup.InitiateReversal("reverse-flow");
        if (reversal.IsFailure)
        {
            return;
        }

        await _outbox.EnqueueReverseRequestedAsync(
            topup.Id,
            topup.BankReference,
            topup.Amount,
            topup.FailureReason.ToString(),
            topup.CorrelationId,
            cancellationToken);

        _logger.LogInformation(
            "درخواست برگشت وجه برای تراکنش {TopupId} به سرویس Payment ارسال شد",
            topup.Id);
    }

    private static TopupFailureReason ClassifyTopupFailure() => TopupFailureReason.TopupProviderError;
}
