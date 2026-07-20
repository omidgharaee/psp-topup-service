namespace PSP.TopupService.Contracts.Events;

/// <summary>Published when a new topup transaction has been created.</summary>
public sealed record TopupCreatedEvent : IIntegrationEvent
{
    public TopupCreatedEvent(Guid topupId, string mobileNumber, decimal amount, string currency, Guid correlationId, DateTime occurredOnUtc)
    {
        TopupId = topupId;
        MobileNumber = mobileNumber;
        Amount = amount;
        Currency = currency;
        CorrelationId = correlationId;
        OccurredOnUtc = occurredOnUtc;
    }

    public Guid TopupId { get; init; }
    public string MobileNumber { get; init; }
    public decimal Amount { get; init; }
    public string Currency { get; init; }
    public Guid CorrelationId { get; init; }
    public DateTime OccurredOnUtc { get; init; }
    public int Version { get; init; } = 1;
    public string EventType => nameof(TopupCreatedEvent);
}

/// <summary>
/// Published to request that the Bank mock process a payment for a topup.
/// Consumed by <c>PSP.Mock.Bank.Api</c>.
/// </summary>
public sealed record PaymentRequestedEvent : IIntegrationEvent
{
    public PaymentRequestedEvent(Guid topupId, decimal amount, string currency, string bankReference, string bankSource, Guid correlationId, DateTime occurredOnUtc)
    {
        TopupId = topupId;
        Amount = amount;
        Currency = currency;
        BankReference = bankReference;
        BankSource = bankSource;
        CorrelationId = correlationId;
        OccurredOnUtc = occurredOnUtc;
    }

    public Guid TopupId { get; init; }
    public decimal Amount { get; init; }
    public string Currency { get; init; }
    public string BankReference { get; init; }
    public string BankSource { get; init; }
    public Guid CorrelationId { get; init; }
    public DateTime OccurredOnUtc { get; init; }
    public int Version { get; init; } = 1;
    public string EventType => nameof(PaymentRequestedEvent);
}

/// <summary>
/// Published by the Bank mock when a payment has been processed (success or failure).
/// Consumed by the Topup service to advance the aggregate.
/// </summary>
public sealed record PaymentCompletedEvent : IIntegrationEvent
{
    public PaymentCompletedEvent(Guid topupId, string bankReference, bool succeeded, string? failureReason, DateTime confirmedAtUtc, Guid correlationId, DateTime occurredOnUtc)
    {
        TopupId = topupId;
        BankReference = bankReference;
        Succeeded = succeeded;
        FailureReason = failureReason;
        ConfirmedAtUtc = confirmedAtUtc;
        CorrelationId = correlationId;
        OccurredOnUtc = occurredOnUtc;
    }

    public Guid TopupId { get; init; }
    public string BankReference { get; init; }
    public bool Succeeded { get; init; }
    public string? FailureReason { get; init; }
    public DateTime ConfirmedAtUtc { get; init; }
    public Guid CorrelationId { get; init; }
    public DateTime OccurredOnUtc { get; init; }
    public int Version { get; init; } = 1;
    public string EventType => nameof(PaymentCompletedEvent);
}

/// <summary>Published when a topup has been completed successfully (terminal).</summary>
public sealed record TopupCompletedEvent : IIntegrationEvent
{
    public TopupCompletedEvent(Guid topupId, string mciReference, string mciSource, Guid correlationId, DateTime occurredOnUtc)
    {
        TopupId = topupId;
        MciReference = mciReference;
        MciSource = mciSource;
        CorrelationId = correlationId;
        OccurredOnUtc = occurredOnUtc;
    }

    public Guid TopupId { get; init; }
    public string MciReference { get; init; }
    public string MciSource { get; init; }
    public Guid CorrelationId { get; init; }
    public DateTime OccurredOnUtc { get; init; }
    public int Version { get; init; } = 1;
    public string EventType => nameof(TopupCompletedEvent);
}

/// <summary>Published when a payment has been reversed after a topup failure (terminal).</summary>
public sealed record PaymentReversedEvent : IIntegrationEvent
{
    public PaymentReversedEvent(Guid topupId, string reversalReference, string reversalSource, string reason, Guid correlationId, DateTime occurredOnUtc)
    {
        TopupId = topupId;
        ReversalReference = reversalReference;
        ReversalSource = reversalSource;
        Reason = reason;
        CorrelationId = correlationId;
        OccurredOnUtc = occurredOnUtc;
    }

    public Guid TopupId { get; init; }
    public string ReversalReference { get; init; }
    public string ReversalSource { get; init; }
    public string Reason { get; init; }
    public Guid CorrelationId { get; init; }
    public DateTime OccurredOnUtc { get; init; }
    public int Version { get; init; } = 1;
    public string EventType => nameof(PaymentReversedEvent);
}
