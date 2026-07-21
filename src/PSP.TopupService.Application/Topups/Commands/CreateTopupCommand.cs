using MediatR;
using PSP.TopupService.Application.Topups.DTOs;

namespace PSP.TopupService.Application.Topups.Commands;

/// <summary>
/// Command to create a new topup transaction. Returns the new transaction's id
/// on success, or a failed <c>Result</c> with a stable error code on validation
/// or idempotency-conflict failures.
/// </summary>
public sealed record CreateTopupCommand : IRequest<CreateTopupResult>
{
    /// <summary>Raw mobile number string as supplied by the caller.</summary>
    public required string MobileNumber { get; init; }

    /// <summary>Amount in Iranian Rial.</summary>
    public required decimal Amount { get; init; }

    /// <summary>Optional idempotency key for safe retries.</summary>
    public string? IdempotencyKey { get; init; }

    /// <summary>Identity of the actor initiating the topup (from the API principal).</summary>
    public string? Actor { get; init; }

    /// <summary>Remote caller IP for audit/anti-fraud purposes.</summary>
    public string? RemoteIp { get; init; }
}

/// <summary>
/// Outcome of <see cref="CreateTopupCommand"/>. Distinguishes a freshly-created
/// transaction from an idempotent replay returning the original one.
/// </summary>
public sealed record CreateTopupResult
{
    public required TopupResponse Response { get; init; }

    /// <summary>True when the result is a replay of a previously-processed request.</summary>
    public bool IsIdempotentReplay { get; init; }

    public static CreateTopupResult Created(TopupResponse response) => new() { Response = response };

    public static CreateTopupResult Replay(TopupResponse response) =>
        new() { Response = response, IsIdempotentReplay = true };
}
