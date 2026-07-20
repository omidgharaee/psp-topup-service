using PSP.TopupService.SharedKernel.Entities;

namespace PSP.TopupService.SharedKernel.Events;

/// <summary>
/// Abstraction responsible for dispatching domain events after the aggregate
/// has been persisted. Implementations live in Infrastructure (MediatR-based
/// or outbox-based). The Domain layer depends only on this contract.
/// </summary>
public interface IDomainEventDispatcher
{
    /// <summary>Dispatches and clears the events currently held by the aggregate.</summary>
    Task DispatchAsync(AggregateRoot<Guid> aggregate, CancellationToken cancellationToken = default);

    /// <summary>Dispatches a single domain event.</summary>
    Task DispatchAsync(IDomainEvent domainEvent, CancellationToken cancellationToken = default);
}
