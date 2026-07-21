using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using PSP.TopupService.Application.Common.Abstractions;
using PSP.TopupService.Application.Common.Outbox;
using PSP.TopupService.Application.Topups.Commands.ApplyReversalResult;
using PSP.TopupService.Domain.Topups;
using PSP.TopupService.Domain.Topups.Enums;
using PSP.TopupService.Domain.Topups.ValueObjects;
using PSP.TopupService.SharedKernel.Exceptions;

namespace PSP.TopupService.UnitTests.Application.Topups.ApplyReversalResult;

/// <summary>
/// Verifies <see cref="ApplyReversalResultCommandHandler"/>: completes the
/// reversal saga by recording the Payment service's reversal reference on the
/// aggregate, transitioning it to terminal Reversed, and publishing the public
/// PaymentReversed event.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Layer", "Application")]
public class ApplyReversalResultCommandHandlerTests
{
    private readonly Mock<ITopupRepository> _repository = new();
    private readonly Mock<IOutboxWriter> _outbox = new();

    [Fact]
    public async Task Handle_Should_Transition_To_Reversed_And_Enqueue_PaymentReversed()
    {
        var tx = AwaitingReversalTopup();
        _repository.Setup(r => r.GetByIdForUpdateAsync(tx.Id, It.IsAny<CancellationToken>())).ReturnsAsync(tx);
        var reversalRef = TransactionReference.Create("REV-1", "REVERSAL");

        var handler = new ApplyReversalResultCommandHandler(_repository.Object, _outbox.Object, NullLogger<ApplyReversalResultCommandHandler>.Instance);
        var result = await handler.Handle(new ApplyReversalResultCommand
        {
            TopupId = tx.Id,
            MessageId = Guid.NewGuid(),
            ReversalReference = reversalRef.Value,
            ReversalSource = reversalRef.Source,
        }, CancellationToken.None);

        result.WasIdempotentReplay.Should().BeFalse();
        result.FinalStatus.Should().Be(nameof(TopupStatus.Reversed));
        tx.Status.Should().Be(TopupStatus.Reversed);
        tx.IsTerminal.Should().BeTrue();
        tx.Reverse!.Status.Should().Be(ReverseStatus.Completed);
        _outbox.Verify(o => o.EnqueuePaymentReversedAsync(
            tx.Id, It.IsAny<TransactionReference>(), tx.FailureReason, tx.CorrelationId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_Should_Replay_When_No_Reversal_Initiated()
    {
        var tx = TopupTransaction.Create(MobileNumber.Create("09121234567"), Money.Create(50_000), Guid.NewGuid(), "actor");
        _repository.Setup(r => r.GetByIdForUpdateAsync(tx.Id, It.IsAny<CancellationToken>())).ReturnsAsync(tx);

        var handler = new ApplyReversalResultCommandHandler(_repository.Object, _outbox.Object, NullLogger<ApplyReversalResultCommandHandler>.Instance);
        var result = await handler.Handle(new ApplyReversalResultCommand
        {
            TopupId = tx.Id,
            MessageId = Guid.NewGuid(),
            ReversalReference = "REV",
            ReversalSource = "REVERSAL",
        }, CancellationToken.None);

        result.WasIdempotentReplay.Should().BeTrue();
        _outbox.Verify(o => o.EnqueuePaymentReversedAsync(It.IsAny<Guid>(), It.IsAny<TransactionReference>(), It.IsAny<TopupFailureReason>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_Should_Replay_When_Already_Reversed()
    {
        var tx = AwaitingReversalTopup();
        tx.MarkPaymentReversed(TransactionReference.Create("REV-OLD", "REVERSAL"), DateTime.UtcNow);
        _repository.Setup(r => r.GetByIdForUpdateAsync(tx.Id, It.IsAny<CancellationToken>())).ReturnsAsync(tx);

        var handler = new ApplyReversalResultCommandHandler(_repository.Object, _outbox.Object, NullLogger<ApplyReversalResultCommandHandler>.Instance);
        var result = await handler.Handle(new ApplyReversalResultCommand
        {
            TopupId = tx.Id,
            MessageId = Guid.NewGuid(),
            ReversalReference = "REV-NEW",
            ReversalSource = "REVERSAL",
        }, CancellationToken.None);

        result.WasIdempotentReplay.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_Should_Throw_NotFound_When_Aggregate_Missing()
    {
        _repository.Setup(r => r.GetByIdForUpdateAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((TopupTransaction?)null);

        var handler = new ApplyReversalResultCommandHandler(_repository.Object, _outbox.Object, NullLogger<ApplyReversalResultCommandHandler>.Instance);
        var act = () => handler.Handle(new ApplyReversalResultCommand
        {
            TopupId = Guid.NewGuid(),
            MessageId = Guid.NewGuid(),
            ReversalReference = "REV",
            ReversalSource = "REVERSAL",
        }, CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    /// <summary>Creates a topup that has failed terminally and initiated a reversal (still in TopupInProgress).</summary>
    private static TopupTransaction AwaitingReversalTopup()
    {
        var tx = TopupTransaction.Create(MobileNumber.Create("09121234567"), Money.Create(50_000), Guid.NewGuid(), "actor");
        tx.MarkPaymentInitiated(TransactionReference.Generate("BANK"));
        tx.MarkPaymentCompleted(tx.BankReference!, DateTime.UtcNow);
        tx.StartTopupAttempt();
        tx.MarkTopupFailed(TopupFailureReason.TopupProviderError, "all retries failed");
        tx.InitiateReversal("reverse-flow");
        return tx;
    }
}
