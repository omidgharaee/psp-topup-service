using MediatR;
using Microsoft.Extensions.Logging;
using PSP.TopupService.Application.Common.Abstractions;
using PSP.TopupService.Application.Common.Outbox;
using PSP.TopupService.Domain.Topups;
using PSP.TopupService.Domain.Topups.ValueObjects;

namespace PSP.TopupService.Application.Topups.Commands.ApplyReversalResult;

/// <summary>
/// Handles <see cref="ApplyReversalResultCommand"/>: completes the reversal
/// saga by recording the Payment service's reversal reference on the aggregate
/// and transitioning it to terminal <c>Reversed</c>.
/// </summary>
public sealed class ApplyReversalResultCommandHandler : IRequestHandler<ApplyReversalResultCommand, ApplyReversalResultResult>
{
    private readonly ITopupRepository _repository;
    private readonly IOutboxWriter _outbox;
    private readonly ILogger<ApplyReversalResultCommandHandler> _logger;

    public ApplyReversalResultCommandHandler(
        ITopupRepository repository,
        IOutboxWriter outbox,
        ILogger<ApplyReversalResultCommandHandler> logger)
    {
        _repository = repository;
        _outbox = outbox;
        _logger = logger;
    }

    public async Task<ApplyReversalResultResult> Handle(ApplyReversalResultCommand request, CancellationToken cancellationToken)
    {
        var topup = await _repository.GetByIdForUpdateAsync(request.TopupId, cancellationToken)
            ?? throw new SharedKernel.Exceptions.NotFoundException(nameof(TopupTransaction), request.TopupId);

        // Idempotency: only an aggregate that has initiated a reversal and has
        // not yet reached Reversed can advance here.
        if (topup.Reverse is null || topup.Status == Domain.Topups.Enums.TopupStatus.Reversed)
        {
            return ApplyReversalResultResult.Replay(topup.Id, topup.Status.ToString());
        }

        var reversalRef = TransactionReference.Create(request.ReversalReference, request.ReversalSource);

        var markResult = topup.MarkPaymentReversed(reversalRef, DateTime.UtcNow);
        if (markResult.IsFailure)
        {
            _logger.LogError("عدم توانایی ثبت برگشت در aggregate - تراکنش {TopupId}", topup.Id);
            return ApplyReversalResultResult.Replay(topup.Id, topup.Status.ToString());
        }

        // Publish the public PaymentReversed terminal event so external
        // subscribers (notification, analytics) see the final outcome.
        await _outbox.EnqueuePaymentReversedAsync(
            topup.Id, reversalRef, topup.FailureReason, topup.CorrelationId, cancellationToken);

        _logger.LogInformation("برگشت وجه انجام شد - تراکنش {TopupId} - مرجع {Reference}", topup.Id, reversalRef);
        return ApplyReversalResultResult.Done(topup.Id, topup.Status.ToString());
    }
}
