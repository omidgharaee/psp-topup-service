using MediatR;
using Moq;
using PSP.TopupService.Application.Common.Abstractions;
using PSP.TopupService.Application.Common.Outbox;
using PSP.TopupService.Application.Topups.Commands.ProcessPaymentResult;
using PSP.TopupService.Domain.Topups;
using PSP.TopupService.Domain.Topups.Enums;
using PSP.TopupService.Domain.Topups.ValueObjects;
using PSP.TopupService.SharedKernel.Exceptions;

namespace PSP.TopupService.UnitTests.Application.Topups.ProcessPaymentResult;

/// <summary>
/// Verifies <see cref="ProcessPaymentResultCommandHandler"/>: success path
/// (PaymentCompleted -> outbox PaymentRequested), failure path (Failed ->
/// outbox PaymentReversed) and idempotency replay when the aggregate has
/// already moved past the awaiting-payment state.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Layer", "Application")]
public class ProcessPaymentResultCommandHandlerTests
{
    private readonly Mock<ITopupRepository> _repository = new();
    private readonly Mock<IOutboxWriter> _outbox = new();

    [Fact]
    public async Task Handle_Success_Should_Transition_To_PaymentCompleted_And_Enqueue_PaymentRequested()
    {
        var tx = PendingPaymentTransaction();
        _repository.Setup(r => r.GetByIdForUpdateAsync(tx.Id, It.IsAny<CancellationToken>())).ReturnsAsync(tx);

        var handler = new ProcessPaymentResultCommandHandler(_repository.Object, _outbox.Object);
        var result = await handler.Handle(SuccessCommand(tx.Id), CancellationToken.None);

        result.WasIdempotentReplay.Should().BeFalse();
        result.NewStatus.Should().Be(nameof(TopupStatus.PaymentCompleted));
        tx.Status.Should().Be(TopupStatus.PaymentCompleted);

        _outbox.Verify(o => o.EnqueuePaymentRequestedAsync(
            tx.Id, It.IsAny<Money>(), It.IsAny<TransactionReference>(), tx.CorrelationId,
            It.IsAny<CancellationToken>()), Times.Once);
        _outbox.Verify(o => o.EnqueuePaymentReversedAsync(
            It.IsAny<Guid>(), It.IsAny<TransactionReference>(), It.IsAny<TopupFailureReason>(),
            It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_Failure_Should_Transition_To_Failed_And_Enqueue_PaymentReversed()
    {
        var tx = PendingPaymentTransaction();
        _repository.Setup(r => r.GetByIdForUpdateAsync(tx.Id, It.IsAny<CancellationToken>())).ReturnsAsync(tx);

        var handler = new ProcessPaymentResultCommandHandler(_repository.Object, _outbox.Object);
        var result = await handler.Handle(FailureCommand(tx.Id, "Bank declined"), CancellationToken.None);

        result.WasIdempotentReplay.Should().BeFalse();
        result.NewStatus.Should().Be(nameof(TopupStatus.Failed));
        tx.Status.Should().Be(TopupStatus.Failed);
        tx.FailureReason.Should().Be(TopupFailureReason.PaymentDeclined);

        _outbox.Verify(o => o.EnqueuePaymentReversedAsync(
            tx.Id, It.IsAny<TransactionReference>(), TopupFailureReason.PaymentDeclined,
            tx.CorrelationId, It.IsAny<CancellationToken>()), Times.Once);
        _outbox.Verify(o => o.EnqueuePaymentRequestedAsync(
            It.IsAny<Guid>(), It.IsAny<Money>(), It.IsAny<TransactionReference>(),
            It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_Failure_With_Timeout_Reason_Should_Classify_As_Timeout()
    {
        var tx = PendingPaymentTransaction();
        _repository.Setup(r => r.GetByIdForUpdateAsync(tx.Id, It.IsAny<CancellationToken>())).ReturnsAsync(tx);

        var handler = new ProcessPaymentResultCommandHandler(_repository.Object, _outbox.Object);
        await handler.Handle(FailureCommand(tx.Id, "Bank TIMEOUT"), CancellationToken.None);

        tx.FailureReason.Should().Be(TopupFailureReason.PaymentTimeout);
    }

    [Fact]
    public async Task Handle_Should_Return_Replay_When_Aggregate_Already_Past_Payment_Phase()
    {
        var tx = CompletedTopup();
        _repository.Setup(r => r.GetByIdForUpdateAsync(tx.Id, It.IsAny<CancellationToken>())).ReturnsAsync(tx);

        var handler = new ProcessPaymentResultCommandHandler(_repository.Object, _outbox.Object);
        var result = await handler.Handle(SuccessCommand(tx.Id), CancellationToken.None);

        result.WasIdempotentReplay.Should().BeTrue();
        result.NewStatus.Should().Be(nameof(TopupStatus.Completed));

        _outbox.Verify(o => o.EnqueuePaymentRequestedAsync(
            It.IsAny<Guid>(), It.IsAny<Money>(), It.IsAny<TransactionReference>(),
            It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_Should_Throw_NotFound_When_Aggregate_Missing()
    {
        _repository.Setup(r => r.GetByIdForUpdateAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((TopupTransaction?)null);

        var handler = new ProcessPaymentResultCommandHandler(_repository.Object, _outbox.Object);

        var act = () => handler.Handle(SuccessCommand(Guid.NewGuid()), CancellationToken.None);
        await act.Should().ThrowAsync<NotFoundException>();
    }

    private static TopupTransaction PendingPaymentTransaction()
    {
        var tx = TopupTransaction.Create(
            MobileNumber.Create("09121234567"),
            Money.Create(50_000),
            Guid.NewGuid(),
            "actor");
        tx.MarkPaymentInitiated(TransactionReference.Generate("BANK"));
        return tx;
    }

    private static TopupTransaction CompletedTopup()
    {
        var tx = PendingPaymentTransaction();
        tx.MarkPaymentCompleted(tx.BankReference!, DateTime.UtcNow);
        tx.StartTopupAttempt();
        tx.Attempts.First().MarkSucceeded("MCI-OK");
        tx.MarkTopupSucceeded(TransactionReference.Create("MCI-OK", "MCI"), DateTime.UtcNow);
        tx.MarkAdviceCompleted(TransactionReference.Create("ADV-1", "ADVICE"), DateTime.UtcNow);
        return tx;
    }

    private static ProcessPaymentResultCommand SuccessCommand(Guid topupId) => new()
    {
        TopupId = topupId,
        MessageId = Guid.NewGuid(),
        BankReference = "BANK-1",
        BankSource = "BANK",
        Succeeded = true,
        ConfirmedAtUtc = DateTime.UtcNow,
    };

    private static ProcessPaymentResultCommand FailureCommand(Guid topupId, string reason) => new()
    {
        TopupId = topupId,
        MessageId = Guid.NewGuid(),
        BankReference = "BANK-1",
        BankSource = "BANK",
        Succeeded = false,
        FailureReason = reason,
        ConfirmedAtUtc = DateTime.UtcNow,
    };
}
