using MediatR;
using PSP.TopupService.Application.Topups.DTOs;

namespace PSP.TopupService.Application.Topups.Commands.ProcessPaymentResult;

/// <summary>
/// Command that processes the asynchronous result of a Bank payment for a
/// topup transaction. Raised by the messaging consumer when a
/// <c>PaymentCompletedEvent</c> arrives. On success the aggregate advances to
/// <c>PaymentCompleted</c> and the outbox is enqueued for the MCI topup call;
/// on failure the aggregate moves terminally to <c>Failed</c>.
/// </summary>
public sealed record ProcessPaymentResultCommand : IRequest<ProcessPaymentResultResult>
{
    /// <summary>The topup aggregate id the payment belongs to.</summary>
    public required Guid TopupId { get; init; }

    /// <summary>The broker message id, used for idempotency (inbox).</summary>
    public required Guid MessageId { get; init; }

    /// <summary>The reference the Bank assigned to the payment.</summary>
    public required string BankReference { get; init; }

    /// <summary>The Bank source tag (e.g. "BANK").</summary>
    public required string BankSource { get; init; }

    /// <summary>True when the Bank confirmed the payment succeeded.</summary>
    public required bool Succeeded { get; init; }

    /// <summary>Failure reason text from the Bank, when Succeeded is false.</summary>
    public string? FailureReason { get; init; }

    /// <summary>UTC instant the Bank confirmed the payment result.</summary>
    public required DateTime ConfirmedAtUtc { get; init; }
}

/// <summary>Outcome of <see cref="ProcessPaymentResultCommand"/>.</summary>
public sealed record ProcessPaymentResultResult
{
    public required Guid TopupId { get; init; }

    public required string NewStatus { get; init; }

    /// <summary>True when the message was a replay (already processed via inbox).</summary>
    public bool WasIdempotentReplay { get; init; }

    public static ProcessPaymentResultResult Processed(Guid topupId, string newStatus) =>
        new() { TopupId = topupId, NewStatus = newStatus };

    public static ProcessPaymentResultResult Replay(Guid topupId, string currentStatus) =>
        new() { TopupId = topupId, NewStatus = currentStatus, WasIdempotentReplay = true };
}
