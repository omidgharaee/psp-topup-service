using MediatR;

namespace PSP.TopupService.Application.Topups.Commands.ApplyAdviceResult;

/// <summary>
/// Command that applies the asynchronous result of a Bank Advice call to the
/// topup aggregate. Raised by <c>AdviceCompletedConsumer</c> when the Payment
/// service publishes <c>AdviceCompletedEvent</c>. On success the aggregate
/// advances to terminal <c>Completed</c> and the <c>TopupCompleted</c> event
/// is published.
/// </summary>
public sealed record ApplyAdviceResultCommand : IRequest<ApplyAdviceResultResult>
{
    /// <summary>The topup aggregate id the advice belongs to.</summary>
    public required Guid TopupId { get; init; }

    /// <summary>The broker message id of the AdviceCompleted event, for inbox.</summary>
    public required Guid MessageId { get; init; }

    /// <summary>The advice reference returned by the Payment service.</summary>
    public required string AdviceReference { get; init; }

    /// <summary>The advice source tag (e.g. "ADVICE").</summary>
    public required string AdviceSource { get; init; }
}

public sealed record ApplyAdviceResultResult
{
    public required Guid TopupId { get; init; }
    public required string FinalStatus { get; init; }
    public bool WasIdempotentReplay { get; init; }

    public static ApplyAdviceResultResult Done(Guid topupId, string finalStatus) => new() { TopupId = topupId, FinalStatus = finalStatus };
    public static ApplyAdviceResultResult Replay(Guid topupId, string currentStatus) =>
        new() { TopupId = topupId, FinalStatus = currentStatus, WasIdempotentReplay = true };
}
