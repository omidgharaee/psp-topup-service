using PSP.TopupService.Domain.Topups.Enums;

namespace PSP.TopupService.Application.Topups.DTOs;

/// <summary>Request body for <c>POST /api/v1/topups</c>.</summary>
public sealed record CreateTopupRequest
{
    /// <summary>Iranian mobile number in <c>09XXXXXXXXX</c> (or +98/0098) form.</summary>
    public string MobileNumber { get; init; } = string.Empty;

    /// <summary>Amount in Iranian Rial; must be within the operator's accepted range.</summary>
    public decimal Amount { get; init; }

    /// <summary>
    /// Optional idempotency key. Two requests with the same key collapse into
    /// one transaction; the response returns the original transaction's id.
    /// </summary>
    public string? IdempotencyKey { get; init; }
}

/// <summary>Response body for the create-topup endpoint.</summary>
public sealed record TopupResponse
{
    /// <summary>The id of the topup transaction.</summary>
    public Guid TransactionId { get; init; }

    /// <summary>Current lifecycle state (Pending on creation).</summary>
    public string Status { get; init; } = string.Empty;

    /// <summary>Canonical mobile number.</summary>
    public string MobileNumber { get; init; } = string.Empty;

    /// <summary>Amount in IRR.</summary>
    public decimal Amount { get; init; }

    /// <summary>UTC creation timestamp.</summary>
    public DateTime CreatedOnUtc { get; init; }
}

/// <summary>Detailed view returned by GET /topups/{id}.</summary>
public sealed record TopupDetailsDto
{
    public Guid TransactionId { get; init; }
    public string Status { get; init; } = string.Empty;
    public string MobileNumber { get; init; } = string.Empty;
    public decimal Amount { get; init; }
    public string Currency { get; init; } = "IRR";
    public Guid CorrelationId { get; init; }
    public string? BankReference { get; init; }
    public string? MciReference { get; init; }
    public TopupFailureReason? FailureReason { get; init; }
    public string? FailureMessage { get; init; }
    public DateTime CreatedOnUtc { get; init; }
    public DateTime? CompletedAtUtc { get; init; }
    public int AttemptCount { get; init; }
}
