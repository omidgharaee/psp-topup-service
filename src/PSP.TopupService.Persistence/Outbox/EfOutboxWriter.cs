using Microsoft.EntityFrameworkCore;
using PSP.TopupService.Application.Common.Outbox;
using PSP.TopupService.Domain.Topups.Enums;
using PSP.TopupService.Domain.Topups.ValueObjects;
using PSP.TopupService.Persistence.Context;
using PSP.TopupService.Persistence.Outbox.Enums;

namespace PSP.TopupService.Persistence.Outbox;

/// <summary>
/// EF Core implementation of <see cref="IOutboxWriter"/>. Inserts rows into the
/// outbox table within the current DbContext transaction; the row is committed
/// atomically with the business state change.
/// </summary>
public sealed class EfOutboxWriter : IOutboxWriter
{
    private readonly TopupDbContext _context;
    private readonly OutboxMessageSerializer _serializer;

    public EfOutboxWriter(TopupDbContext context, OutboxMessageSerializer serializer)
    {
        _context = context;
        _serializer = serializer;
    }

    public Task EnqueueTopupCreatedAsync(Guid topupId, MobileNumber mobileNumber, Money amount, Guid correlationId, CancellationToken cancellationToken = default)
    {
        var payload = _serializer.SerializeTopupCreated(topupId, mobileNumber, amount, correlationId);
        return AddAsync(OutboxRoutes.TopupEventsExchange, payload, correlationId, cancellationToken);
    }

    public Task EnqueuePaymentRequestedAsync(Guid topupId, Money amount, TransactionReference bankReference, Guid correlationId, CancellationToken cancellationToken = default)
    {
        var payload = _serializer.SerializePaymentRequested(topupId, amount, bankReference, correlationId);
        return AddAsync(OutboxRoutes.PaymentRequestedRoutingKey, payload, correlationId, cancellationToken);
    }

    public Task EnqueueTopupCompletedAsync(Guid topupId, TransactionReference mciReference, Guid correlationId, CancellationToken cancellationToken = default)
    {
        var payload = _serializer.SerializeTopupCompleted(topupId, mciReference, correlationId);
        return AddAsync(OutboxRoutes.TopupEventsExchange, payload, correlationId, cancellationToken);
    }

    public Task EnqueueAdviceRequestedAsync(
        Guid topupId,
        TransactionReference originalPaymentReference,
        Money amount,
        Guid correlationId,
        DateTime? processAfterUtc,
        int adviceAttempt,
        CancellationToken cancellationToken = default)
    {
        var payload = _serializer.SerializeAdviceRequested(topupId, originalPaymentReference, amount, correlationId, adviceAttempt);
        return AddAsync(OutboxRoutes.AdviceRequestedRoutingKey, payload, correlationId, cancellationToken, processAfterUtc);
    }

    public Task EnqueuePaymentReversedAsync(Guid topupId, TransactionReference reversalReference, TopupFailureReason reason, Guid correlationId, CancellationToken cancellationToken = default)
    {
        var payload = _serializer.SerializePaymentReversed(topupId, reversalReference, reason, correlationId);
        return AddAsync(OutboxRoutes.TopupEventsExchange, payload, correlationId, cancellationToken);
    }

    public Task EnqueueReverseRequestedAsync(Guid topupId, TransactionReference originalPaymentReference, Money amount, string reason, Guid correlationId, CancellationToken cancellationToken = default)
    {
        var payload = _serializer.SerializeReverseRequested(topupId, originalPaymentReference, amount, reason, correlationId);
        return AddAsync(OutboxRoutes.ReverseRequestedRoutingKey, payload, correlationId, cancellationToken);
    }

    private async Task AddAsync(string routingKey, string payload, Guid correlationId, CancellationToken cancellationToken, DateTime? processAfterUtc = null)
    {
        var message = new OutboxMessage
        {
            Type = ExtractTypeFromPayload(payload),
            Payload = payload,
            CorrelationId = correlationId,
            RoutingKey = routingKey,
            Status = OutboxMessageStatus.Pending,
            OccurredOnUtc = DateTime.UtcNow,
            LockedUntilUtc = processAfterUtc,
        };
        await _context.OutboxMessages.AddAsync(message, cancellationToken);
    }

    private static string ExtractTypeFromPayload(string payload)
    {
        // Payload is built by the serializer with a known "$type" property.
        // Extract it so the publisher can route without parsing the whole body.
        var idx = payload.IndexOf("\"$type\":\"", StringComparison.Ordinal);
        if (idx < 0)
        {
            return "Unknown";
        }

        var start = idx + "\"$type\":\"".Length;
        var end = payload.IndexOf('"', start);
        return end < 0 ? "Unknown" : payload[start..end];
    }
}

/// <summary>Routing key / exchange names used by the publisher.</summary>
public static class OutboxRoutes
{
    public const string TopupEventsExchange = "topup.events";
    public const string PaymentRequestedRoutingKey = "payment.requests";
    public const string AdviceRequestedRoutingKey = "advice.requests";
    public const string ReverseRequestedRoutingKey = "reverse.requests";
}
