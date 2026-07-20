using PSP.TopupService.Domain.Topups.Enums;
using PSP.TopupService.Domain.Topups.ValueObjects;

namespace PSP.TopupService.Application.Common.Outbox;

/// <summary>
/// Enqueues an integration event into the transactional outbox. The enqueue is
/// part of the current unit of work so that the business state change and the
/// intent to publish the event are committed together. The Worker later picks
/// up the message and publishes it to RabbitMQ.
/// </summary>
public interface IOutboxWriter
{
    /// <summary>Enqueues a topup-related integration event for asynchronous publication.</summary>
    Task EnqueueTopupCreatedAsync(
        Guid topupId,
        MobileNumber mobileNumber,
        Money amount,
        Guid correlationId,
        CancellationToken cancellationToken = default);

    /// <summary>Enqueues the <c>PaymentRequested</c> event that triggers the Bank mock to process a payment.</summary>
    Task EnqueuePaymentRequestedAsync(
        Guid topupId,
        Money amount,
        TransactionReference bankReference,
        Guid correlationId,
        CancellationToken cancellationToken = default);

    /// <summary>Enqueues the <c>TopupCompleted</c> terminal success event.</summary>
    Task EnqueueTopupCompletedAsync(
        Guid topupId,
        TransactionReference mciReference,
        Guid correlationId,
        CancellationToken cancellationToken = default);

    /// <summary>Enqueues the <c>PaymentReversed</c> terminal failure event.</summary>
    Task EnqueuePaymentReversedAsync(
        Guid topupId,
        TransactionReference reversalReference,
        TopupFailureReason reason,
        Guid correlationId,
        CancellationToken cancellationToken = default);
}
