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
        var payload = _serializer.SerializeTopupCreated(topupId, mobileNumber, amount);
        return AddAsync(OutboxRoutes.TopupEventsExchange, payload, correlationId, cancellationToken);
    }

    public Task EnqueuePaymentRequestedAsync(Guid topupId, Money amount, TransactionReference bankReference, Guid correlationId, CancellationToken cancellationToken = default)
    {
        var payload = _serializer.SerializePaymentRequested(topupId, amount, bankReference);
        return AddAsync(OutboxRoutes.PaymentRequestedRoutingKey, payload, correlationId, cancellationToken);
    }

    public Task EnqueueTopupCompletedAsync(Guid topupId, TransactionReference mciReference, Guid correlationId, CancellationToken cancellationToken = default)
    {
        var payload = _serializer.SerializeTopupCompleted(topupId, mciReference);
        return AddAsync(OutboxRoutes.TopupEventsExchange, payload, correlationId, cancellationToken);
    }

    public Task EnqueuePaymentReversedAsync(Guid topupId, TransactionReference reversalReference, TopupFailureReason reason, Guid correlationId, CancellationToken cancellationToken = default)
    {
        var payload = _serializer.SerializePaymentReversed(topupId, reversalReference, reason);
        return AddAsync(OutboxRoutes.TopupEventsExchange, payload, correlationId, cancellationToken);
    }

    private async Task AddAsync(string routingKey, string payload, Guid correlationId, CancellationToken cancellationToken)
    {
        var message = new OutboxMessage
        {
            Type = ExtractTypeFromPayload(payload),
            Payload = payload,
            CorrelationId = correlationId,
            RoutingKey = routingKey,
            Status = OutboxMessageStatus.Pending,
            OccurredOnUtc = DateTime.UtcNow,
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
}
