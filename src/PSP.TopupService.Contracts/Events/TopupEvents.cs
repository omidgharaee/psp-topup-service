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

/// <summary>
/// Published to request that the Bank finalize (advice) the payment for a
/// topup whose MCI step has succeeded. Consumed by the advice worker which
/// retries via outbox scheduling until success or terminal failure.
/// </summary>
public sealed record AdviceRequestedEvent : IIntegrationEvent
{
    public AdviceRequestedEvent(Guid topupId, string originalReference, string originalSource, decimal amount, string currency, int adviceAttempt, Guid correlationId, DateTime occurredOnUtc)
    {
        TopupId = topupId;
        OriginalReference = originalReference;
        OriginalSource = originalSource;
        Amount = amount;
        Currency = currency;
        AdviceAttempt = adviceAttempt;
        CorrelationId = correlationId;
        OccurredOnUtc = occurredOnUtc;
    }

    public Guid TopupId { get; init; }
    public string OriginalReference { get; init; }
    public string OriginalSource { get; init; }
    public decimal Amount { get; init; }
    public string Currency { get; init; }
    public int AdviceAttempt { get; init; }
    public Guid CorrelationId { get; init; }
    public DateTime OccurredOnUtc { get; init; }
    public int Version { get; init; } = 1;
    public string EventType => nameof(AdviceRequestedEvent);
}

/// <summary>
/// Published by the Bank mock when the Advice (finalization) succeeds. Consumed
/// by the Topup service to advance the aggregate to terminal Completed.
/// </summary>
public sealed record AdviceCompletedEvent : IIntegrationEvent
{
    public AdviceCompletedEvent(Guid topupId, string adviceReference, string adviceSource, Guid correlationId, DateTime occurredOnUtc)
    {
        TopupId = topupId;
        AdviceReference = adviceReference;
        AdviceSource = adviceSource;
        CorrelationId = correlationId;
        OccurredOnUtc = occurredOnUtc;
    }

    public Guid TopupId { get; init; }
    public string AdviceReference { get; init; }
    public string AdviceSource { get; init; }
    public Guid CorrelationId { get; init; }
    public DateTime OccurredOnUtc { get; init; }
    public int Version { get; init; } = 1;
    public string EventType => nameof(AdviceCompletedEvent);
}

/// <summary>
/// Published by the Topup service to request that the Bank reverse a payment
/// after a terminal topup failure. Consumed by the Bank mock.
/// </summary>
public sealed record ReverseRequestedEvent : IIntegrationEvent
{
    public ReverseRequestedEvent(Guid topupId, string originalReference, string originalSource, decimal amount, string currency, string reason, Guid correlationId, DateTime occurredOnUtc)
    {
        TopupId = topupId;
        OriginalReference = originalReference;
        OriginalSource = originalSource;
        Amount = amount;
        Currency = currency;
        Reason = reason;
        CorrelationId = correlationId;
        OccurredOnUtc = occurredOnUtc;
    }

    public Guid TopupId { get; init; }
    public string OriginalReference { get; init; }
    public string OriginalSource { get; init; }
    public decimal Amount { get; init; }
    public string Currency { get; init; }
    public string Reason { get; init; }
    public Guid CorrelationId { get; init; }
    public DateTime OccurredOnUtc { get; init; }
    public int Version { get; init; } = 1;
    public string EventType => nameof(ReverseRequestedEvent);
}
