using MediatR;

namespace PSP.TopupService.Application.Topups.Commands.PerformTopup;

/// <summary>
/// Command that performs the actual mobile topup against Hamrah-e-Aval, and —
/// if that fails terminally — triggers the Bank reversal. This is the heart of
/// the topup flow: payment is already settled, so a provider failure MUST
/// reverse the money or we lose it. The whole operation is wrapped in the
/// TransactionBehavior pipeline so all changes (and outbox events) commit
/// together.
/// </summary>
public sealed record PerformTopupCommand : IRequest<PerformTopupResult>
{
    /// <summary>The topup aggregate id to operate on.</summary>
    public required Guid TopupId { get; init; }

    /// <summary>The broker message id (PaymentRequested), for idempotency.</summary>
    public required Guid MessageId { get; init; }
}

/// <summary>Outcome of <see cref="PerformTopupCommand"/>.</summary>
public sealed record PerformTopupResult
{
    public required Guid TopupId { get; init; }

    /// <summary>Terminal status the aggregate reached.</summary>
    public required string FinalStatus { get; init; }

    public bool WasIdempotentReplay { get; init; }

    public static PerformTopupResult Done(Guid topupId, string finalStatus) => new() { TopupId = topupId, FinalStatus = finalStatus };

    public static PerformTopupResult Replay(Guid topupId, string currentStatus) =>
        new() { TopupId = topupId, FinalStatus = currentStatus, WasIdempotentReplay = true };
}
