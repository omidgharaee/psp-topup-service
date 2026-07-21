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

    /// <summary>
    /// Enqueues the <c>AdviceRequested</c> event that triggers the Bank Advice
    /// (finalization) call. The first enqueue is immediate; subsequent retries
    /// are scheduled with a non-null <paramref name="processAfterUtc"/> so the
    /// outbox publisher only re-publishes them after the delay elapses.
    /// </summary>
    Task EnqueueAdviceRequestedAsync(
        Guid topupId,
        TransactionReference originalPaymentReference,
        Money amount,
        Guid correlationId,
        DateTime? processAfterUtc,
        int adviceAttempt,
        CancellationToken cancellationToken = default);

    /// <summary>Enqueues the <c>PaymentReversed</c> terminal failure event.</summary>
    Task EnqueuePaymentReversedAsync(
        Guid topupId,
        TransactionReference reversalReference,
        TopupFailureReason reason,
        Guid correlationId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Enqueues the <c>ReverseRequested</c> event that asks the Payment service
    /// to reverse a settled payment after a terminal topup failure. The Payment
    /// service responds asynchronously with <c>PaymentReversedEvent</c>.
    /// </summary>
    Task EnqueueReverseRequestedAsync(
        Guid topupId,
        TransactionReference originalPaymentReference,
        Money amount,
        string reason,
        Guid correlationId,
        CancellationToken cancellationToken = default);
}
