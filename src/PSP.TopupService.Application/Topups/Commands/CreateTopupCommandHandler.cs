using MediatR;
using PSP.TopupService.Application.Common.Abstractions;
using PSP.TopupService.Application.Common.Context;
using PSP.TopupService.Application.Common.Outbox;
using PSP.TopupService.Application.Topups.DTOs;
using PSP.TopupService.Domain.Topups;
using PSP.TopupService.Domain.Topups.ValueObjects;
using PSP.TopupService.SharedKernel.Results;

namespace PSP.TopupService.Application.Topups.Commands;

/// <summary>
/// Handles <see cref="CreateTopupCommand"/>: creates the topup aggregate, runs
/// idempotency, and enqueues the outbox event. The atomic commit is delegated
/// to <c>TransactionBehavior</c> so the aggregate state and the publish intent
/// can never diverge.
/// </summary>
public sealed class CreateTopupCommandHandler : IRequestHandler<CreateTopupCommand, CreateTopupResult>
{
    private readonly ITopupRepository _repository;
    private readonly IOutboxWriter _outbox;
    private readonly ICorrelationContext _correlation;

    public CreateTopupCommandHandler(
        ITopupRepository repository,
        IOutboxWriter outbox,
        ICorrelationContext correlation)
    {
        _repository = repository;
        _outbox = outbox;
        _correlation = correlation;
    }

    public async Task<CreateTopupResult> Handle(CreateTopupCommand request, CancellationToken cancellationToken)
    {
        // 1. Idempotency: if a previous request with the same key succeeded,
        //    return the original transaction without re-executing side effects.
        if (!string.IsNullOrWhiteSpace(request.IdempotencyKey))
        {
            var existing = await _repository.GetByIdempotencyKeyAsync(request.IdempotencyKey, cancellationToken);
            if (existing is not null)
            {
                return CreateTopupResult.Replay(ToResponse(existing));
            }
        }

        // 2. Construct the value objects (semantic validation; throws on invalid).
        var mobile = MobileNumber.Create(request.MobileNumber);
        var amount = Money.Create(request.Amount);
        var correlationId = _correlation.CorrelationId == Guid.Empty ? Guid.NewGuid() : _correlation.CorrelationId;
        var actor = request.Actor ?? _correlation.Actor ?? "anonymous";

        // 3. Create the aggregate in the Pending state (raises TopupCreatedEvent).
        var topup = TopupTransaction.Create(mobile, amount, correlationId, actor, request.IdempotencyKey);

        // 4. Track the aggregate and enqueue the outbox event in the unit of work.
        //    NOTE: the TransactionBehavior owns the atomic commit so that the
        //    aggregate state and the outbox message commit together.
        await _repository.AddAsync(topup, cancellationToken);
        await _outbox.EnqueueTopupCreatedAsync(topup.Id, topup.MobileNumber, topup.Amount, topup.CorrelationId, cancellationToken);

        return CreateTopupResult.Created(ToResponse(topup));
    }

    private static TopupResponse ToResponse(TopupTransaction topup) => new()
    {
        TransactionId = topup.Id,
        Status = topup.Status.ToString(),
        MobileNumber = topup.MobileNumber.Value,
        Amount = topup.Amount.Value,
        CreatedOnUtc = topup.CreatedOnUtc,
    };
}
