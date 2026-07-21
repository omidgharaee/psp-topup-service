using MediatR;

namespace PSP.TopupService.Application.Topups.Commands.ApplyReversalResult;

/// <summary>
/// Command that applies the asynchronous result of a Payment reversal to the
/// topup aggregate. Raised by <c>PaymentReversedConsumer</c> when the Payment
/// service publishes <c>PaymentReversedEvent</c> in response to a
/// <c>ReverseRequestedEvent</c>. On success the aggregate moves to terminal
/// <c>Reversed</c>.
/// </summary>
public sealed record ApplyReversalResultCommand : IRequest<ApplyReversalResultResult>
{
    public required Guid TopupId { get; init; }

    /// <summary>The broker message id of the PaymentReversed event, for inbox.</summary>
    public required Guid MessageId { get; init; }

    public required string ReversalReference { get; init; }

    public required string ReversalSource { get; init; }
}

public sealed record ApplyReversalResultResult
{
    public required Guid TopupId { get; init; }
    public required string FinalStatus { get; init; }
    public bool WasIdempotentReplay { get; init; }

    public static ApplyReversalResultResult Done(Guid topupId, string finalStatus) => new() { TopupId = topupId, FinalStatus = finalStatus };
    public static ApplyReversalResultResult Replay(Guid topupId, string currentStatus) =>
        new() { TopupId = topupId, FinalStatus = currentStatus, WasIdempotentReplay = true };
}
