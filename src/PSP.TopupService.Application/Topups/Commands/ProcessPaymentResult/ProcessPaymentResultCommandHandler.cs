using MediatR;
using PSP.TopupService.Application.Common.Abstractions;
using PSP.TopupService.Application.Common.Outbox;
using PSP.TopupService.Domain.Topups;
using PSP.TopupService.Domain.Topups.Enums;
using PSP.TopupService.Domain.Topups.ValueObjects;
using PSP.TopupService.SharedKernel.Results;

namespace PSP.TopupService.Application.Topups.Commands.ProcessPaymentResult;

/// <summary>
/// Handles <see cref="ProcessPaymentResultCommand"/>: applies the Bank's
/// asynchronous payment result to the topup aggregate and enqueues the next
/// outbox event. The handler is the single place where the payment result
/// translates to a domain transition; the messaging consumer just dispatches
/// the command.
/// </summary>
public sealed class ProcessPaymentResultCommandHandler : IRequestHandler<ProcessPaymentResultCommand, ProcessPaymentResultResult>
{
    private readonly ITopupRepository _repository;
    private readonly IOutboxWriter _outbox;

    public ProcessPaymentResultCommandHandler(ITopupRepository repository, IOutboxWriter outbox)
    {
        _repository = repository;
        _outbox = outbox;
    }

    public async Task<ProcessPaymentResultResult> Handle(ProcessPaymentResultCommand request, CancellationToken cancellationToken)
    {
        // 1. Load the aggregate with a pessimistic lock so concurrent
        //    redelivered messages cannot race on the same row.
        var topup = await _repository.GetByIdForUpdateAsync(request.TopupId, cancellationToken);
        if (topup is null)
        {
            throw new SharedKernel.Exceptions.NotFoundException(nameof(TopupTransaction), request.TopupId);
        }

        // 2. Idempotency at the aggregate level: if the payment has already
        //    been resolved (Completed/Failed/etc.), the message is a replay.
        if (topup.Status != TopupStatus.Pending && topup.Status != TopupStatus.PaymentInProgress)
        {
            return ProcessPaymentResultResult.Replay(topup.Id, topup.Status.ToString());
        }

        var bankReference = TransactionReference.Create(request.BankReference, request.BankSource);

        // 3. Apply the domain transition.
        Result transition;
        if (request.Succeeded)
        {
            transition = topup.MarkPaymentCompleted(bankReference, request.ConfirmedAtUtc);
        }
        else
        {
            var reason = ClassifyFailure(request.FailureReason);
            transition = topup.MarkPaymentFailed(reason, request.FailureReason ?? "Bank declined the payment.");
        }

        if (transition.IsFailure)
        {
            // The aggregate rejected the transition; treat as a no-op replay so
            // the consumer acknowledges the message rather than retrying.
            return ProcessPaymentResultResult.Replay(topup.Id, topup.Status.ToString());
        }

        // 4. Enqueue the next outbox event.
        if (request.Succeeded)
        {
            // Payment succeeded -> request the MCI topup via the same outbox.
            await _outbox.EnqueuePaymentRequestedAsync(
                topup.Id,
                topup.Amount,
                bankReference,
                topup.CorrelationId,
                cancellationToken);
        }
        else
        {
            // Payment failed terminally -> publish the reversal intent. The
            // reverse-payment feature will fill in the actual reversal reference.
            await _outbox.EnqueuePaymentReversedAsync(
                topup.Id,
                TransactionReference.Generate("REVERSAL"),
                topup.FailureReason,
                topup.CorrelationId,
                cancellationToken);
        }

        // 5. The TransactionBehavior owns the atomic commit.
        return ProcessPaymentResultResult.Processed(topup.Id, topup.Status.ToString());
    }

    private static TopupFailureReason ClassifyFailure(string? failureReason)
    {
        if (string.IsNullOrWhiteSpace(failureReason))
        {
            return TopupFailureReason.PaymentDeclined;
        }

        var upper = failureReason.ToUpperInvariant();
        if (upper.Contains("TIMEOUT", StringComparison.Ordinal))
        {
            return TopupFailureReason.PaymentTimeout;
        }

        if (upper.Contains("DECLINED", StringComparison.Ordinal) || upper.Contains("INSUFFICIENT", StringComparison.Ordinal))
        {
            return TopupFailureReason.PaymentDeclined;
        }

        return TopupFailureReason.PaymentDeclined;
    }
}
