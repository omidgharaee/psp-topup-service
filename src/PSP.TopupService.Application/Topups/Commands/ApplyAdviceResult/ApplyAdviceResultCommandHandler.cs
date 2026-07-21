using MediatR;
using Microsoft.Extensions.Logging;
using PSP.TopupService.Application.Common.Abstractions;
using PSP.TopupService.Application.Common.Outbox;
using PSP.TopupService.Domain.Topups;
using PSP.TopupService.Domain.Topups.ValueObjects;

namespace PSP.TopupService.Application.Topups.Commands.ApplyAdviceResult;

/// <summary>
/// Handles <see cref="ApplyAdviceResultCommand"/>: applies the Payment
/// service's advice confirmation to the aggregate. This is the only path
/// through which a topup can reach terminal <c>Completed</c>.
/// </summary>
public sealed class ApplyAdviceResultCommandHandler : IRequestHandler<ApplyAdviceResultCommand, ApplyAdviceResultResult>
{
    private readonly ITopupRepository _repository;
    private readonly IOutboxWriter _outbox;
    private readonly ILogger<ApplyAdviceResultCommandHandler> _logger;

    public ApplyAdviceResultCommandHandler(
        ITopupRepository repository,
        IOutboxWriter outbox,
        ILogger<ApplyAdviceResultCommandHandler> logger)
    {
        _repository = repository;
        _outbox = outbox;
        _logger = logger;
    }

    public async Task<ApplyAdviceResultResult> Handle(ApplyAdviceResultCommand request, CancellationToken cancellationToken)
    {
        var topup = await _repository.GetByIdForUpdateAsync(request.TopupId, cancellationToken)
            ?? throw new SharedKernel.Exceptions.NotFoundException(nameof(TopupTransaction), request.TopupId);

        // Idempotency: only an aggregate still in AdvicePending can advance.
        if (topup.Status != Domain.Topups.Enums.TopupStatus.AdvicePending)
        {
            return ApplyAdviceResultResult.Replay(topup.Id, topup.Status.ToString());
        }

        var adviceRef = TransactionReference.Create(request.AdviceReference, request.AdviceSource);

        var completion = topup.MarkAdviceCompleted(adviceRef, DateTime.UtcNow);
        if (completion.IsFailure)
        {
            return ApplyAdviceResultResult.Replay(topup.Id, topup.Status.ToString());
        }

        // Now genuinely terminal-successful. Publish TopupCompleted so external
        // subscribers (notification, analytics, etc.) see the success.
        await _outbox.EnqueueTopupCompletedAsync(topup.Id, topup.MciReference!, topup.CorrelationId, cancellationToken);

        _logger.LogInformation("تأیید بانک (Advice) موفق بود - تراکنش {TopupId} تکمیل شد", topup.Id);
        return ApplyAdviceResultResult.Done(topup.Id, topup.Status.ToString());
    }
}
