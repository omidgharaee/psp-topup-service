using MediatR;

namespace PSP.TopupService.Application.Topups.Commands.PerformAdvice;

/// <summary>
/// Command that performs the Bank Advice (finalization) for a topup whose MCI
/// step has succeeded. On success the aggregate moves to its terminal Completed
/// state and the TopupCompleted event is published. On failure the handler
/// re-schedules an advice retry via the outbox (Saga compensation) until
/// MaxAdviceRetries is reached, at which point the transaction is flagged
/// AdviceFailed for manual reconciliation — it is NEVER auto-reversed because
/// the customer has already been topped up.
/// </summary>
public sealed record PerformAdviceCommand : IRequest<PerformAdviceResult>
{
    public required Guid TopupId { get; init; }

    /// <summary>The broker message id of the AdviceRequested event, for inbox.</summary>
    public required Guid MessageId { get; init; }

    /// <summary>0-based advice attempt index (retries increment this).</summary>
    public int AdviceAttempt { get; init; }
}

public sealed record PerformAdviceResult
{
    public required Guid TopupId { get; init; }

    public required string FinalStatus { get; init; }

    public bool WasIdempotentReplay { get; init; }

    public static PerformAdviceResult Done(Guid topupId, string finalStatus) => new() { TopupId = topupId, FinalStatus = finalStatus };

    public static PerformAdviceResult Replay(Guid topupId, string currentStatus) =>
        new() { TopupId = topupId, FinalStatus = currentStatus, WasIdempotentReplay = true };
}
