using MediatR;
using Microsoft.Extensions.Logging;
using PSP.TopupService.Application.Common.Abstractions;
using PSP.TopupService.Application.Common.Outbox;
using PSP.TopupService.Domain.Topups;

namespace PSP.TopupService.Application.Topups.Commands.PerformAdvice;

/// <summary>
/// Handles <see cref="PerformAdviceCommand"/>: dispatches an AdviceRequested
/// integration event to the Bank (Payment Application) via the outbox. The Bank
/// responds asynchronously with AdviceCompletedEvent, which a separate consumer
/// applies to the aggregate. This handler does NOT call the Bank directly —
/// all bank communication is via RabbitMQ so the Topup service never takes a
/// synchronous dependency on the Payment Application.
///
/// Retries are scheduled via the outbox: each retry re-enqueues an
/// AdviceRequestedEvent with an incremented attempt index and a processAfterUtc
/// delay. After MaxRetries the aggregate is flagged AdviceFailed for manual
/// reconciliation — NEVER auto-reversed (the customer has been topped up).
/// </summary>
public sealed class PerformAdviceCommandHandler : IRequestHandler<PerformAdviceCommand, PerformAdviceResult>
{
    private readonly ITopupRepository _repository;
    private readonly IOutboxWriter _outbox;
    private readonly AdviceOptions _options;
    private readonly ILogger<PerformAdviceCommandHandler> _logger;

    public PerformAdviceCommandHandler(
        ITopupRepository repository,
        IOutboxWriter outbox,
        Microsoft.Extensions.Options.IOptions<AdviceOptions> options,
        ILogger<PerformAdviceCommandHandler> logger)
    {
        _repository = repository;
        _outbox = outbox;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<PerformAdviceResult> Handle(PerformAdviceCommand request, CancellationToken cancellationToken)
    {
        var topup = await _repository.GetByIdForUpdateAsync(request.TopupId, cancellationToken)
            ?? throw new SharedKernel.Exceptions.NotFoundException(nameof(TopupTransaction), request.TopupId);

        // Idempotency: only act when the aggregate is awaiting advice.
        if (topup.Status != Domain.Topups.Enums.TopupStatus.AdvicePending)
        {
            return PerformAdviceResult.Replay(topup.Id, topup.Status.ToString());
        }

        if (topup.BankReference is null)
        {
            _logger.LogError("امکان Advice وجود ندارد: BankReference نال است - تراکنش {TopupId}", topup.Id);
            return PerformAdviceResult.Replay(topup.Id, topup.Status.ToString());
        }

        // Record the attempt on the aggregate (flags AdviceFailed at MaxRetries).
        var failResult = topup.MarkAdviceAttemptFailed("Advice dispatched; awaiting bank response", _options.MaxRetries);
        if (failResult.IsFailure)
        {
            return PerformAdviceResult.Replay(topup.Id, topup.Status.ToString());
        }

        if (topup.FailureReason == Domain.Topups.Enums.TopupFailureReason.AdviceFailed)
        {
            _logger.LogError("Advice پس از {Attempts} تلاش شکست خورد - تراکنش {TopupId} نیاز به بررسی دستی دارد", _options.MaxRetries, topup.Id);
            return PerformAdviceResult.Done(topup.Id, topup.Status.ToString());
        }

        // Dispatch the advice request to the Bank via RabbitMQ. The Bank will
        // respond with AdviceCompletedEvent (consumed by AdviceCompletedConsumer).
        await _outbox.EnqueueAdviceRequestedAsync(
            topup.Id,
            topup.BankReference,
            topup.Amount,
            topup.CorrelationId,
            processAfterUtc: null,
            adviceAttempt: request.AdviceAttempt,
            cancellationToken);

        _logger.LogInformation(
            "درخواست Advice برای تراکنش {TopupId} به بانک ارسال شد (تلاش {Attempt})",
            topup.Id,
            request.AdviceAttempt + 1);

        return PerformAdviceResult.Done(topup.Id, topup.Status.ToString());
    }
}
