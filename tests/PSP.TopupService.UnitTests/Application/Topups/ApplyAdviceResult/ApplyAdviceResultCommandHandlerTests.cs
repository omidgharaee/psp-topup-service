using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using PSP.TopupService.Application.Common.Abstractions;
using PSP.TopupService.Application.Common.Outbox;
using PSP.TopupService.Application.Topups.Commands.ApplyAdviceResult;
using PSP.TopupService.Domain.Topups;
using PSP.TopupService.Domain.Topups.Enums;
using PSP.TopupService.Domain.Topups.ValueObjects;
using PSP.TopupService.SharedKernel.Exceptions;

namespace PSP.TopupService.UnitTests.Application.Topups.ApplyAdviceResult;

/// <summary>
/// Verifies <see cref="ApplyAdviceResultCommandHandler"/>: applies the
/// Payment service's advice confirmation to the aggregate and publishes the
/// terminal TopupCompleted event. This is the only path to terminal Completed.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Layer", "Application")]
public class ApplyAdviceResultCommandHandlerTests
{
    private readonly Mock<ITopupRepository> _repository = new();
    private readonly Mock<IOutboxWriter> _outbox = new();

    [Fact]
    public async Task Handle_Should_Complete_Topup_And_Enqueue_TopupCompleted()
    {
        var tx = AdvicePendingTransaction();
        _repository.Setup(r => r.GetByIdForUpdateAsync(tx.Id, It.IsAny<CancellationToken>())).ReturnsAsync(tx);
        var adviceRef = TransactionReference.Create("ADV-1", "ADVICE");

        var handler = new ApplyAdviceResultCommandHandler(_repository.Object, _outbox.Object, NullLogger<ApplyAdviceResultCommandHandler>.Instance);
        var result = await handler.Handle(new ApplyAdviceResultCommand
        {
            TopupId = tx.Id,
            MessageId = Guid.NewGuid(),
            AdviceReference = adviceRef.Value,
            AdviceSource = adviceRef.Source,
        }, CancellationToken.None);

        result.WasIdempotentReplay.Should().BeFalse();
        result.FinalStatus.Should().Be(nameof(TopupStatus.Completed));
        tx.Status.Should().Be(TopupStatus.Completed);
        tx.IsTerminal.Should().BeTrue();
        tx.BankReference.Should().Be(adviceRef);
        _outbox.Verify(o => o.EnqueueTopupCompletedAsync(tx.Id, tx.MciReference!, tx.CorrelationId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_Should_Replay_When_Aggregate_Not_AdvicePending()
    {
        var tx = CompletedTopup();
        _repository.Setup(r => r.GetByIdForUpdateAsync(tx.Id, It.IsAny<CancellationToken>())).ReturnsAsync(tx);

        var handler = new ApplyAdviceResultCommandHandler(_repository.Object, _outbox.Object, NullLogger<ApplyAdviceResultCommandHandler>.Instance);
        var result = await handler.Handle(new ApplyAdviceResultCommand
        {
            TopupId = tx.Id,
            MessageId = Guid.NewGuid(),
            AdviceReference = "ADV-2",
            AdviceSource = "ADVICE",
        }, CancellationToken.None);

        result.WasIdempotentReplay.Should().BeTrue();
        _outbox.Verify(o => o.EnqueueTopupCompletedAsync(It.IsAny<Guid>(), It.IsAny<TransactionReference>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_Should_Throw_NotFound_When_Aggregate_Missing()
    {
        _repository.Setup(r => r.GetByIdForUpdateAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((TopupTransaction?)null);

        var handler = new ApplyAdviceResultCommandHandler(_repository.Object, _outbox.Object, NullLogger<ApplyAdviceResultCommandHandler>.Instance);
        var act = () => handler.Handle(new ApplyAdviceResultCommand
        {
            TopupId = Guid.NewGuid(),
            MessageId = Guid.NewGuid(),
            AdviceReference = "ADV",
            AdviceSource = "ADVICE",
        }, CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    private static TopupTransaction AdvicePendingTransaction()
    {
        var tx = TopupTransaction.Create(MobileNumber.Create("09121234567"), Money.Create(50_000), Guid.NewGuid(), "actor");
        tx.MarkPaymentInitiated(TransactionReference.Generate("BANK"));
        tx.MarkPaymentCompleted(tx.BankReference!, DateTime.UtcNow);
        var attempt = tx.StartTopupAttempt().Value!;
        attempt.MarkSucceeded("MCI-OK");
        tx.MarkTopupSucceeded(TransactionReference.Create("MCI-OK", "MCI"), DateTime.UtcNow);
        return tx;
    }

    private static TopupTransaction CompletedTopup()
    {
        var tx = AdvicePendingTransaction();
        tx.MarkAdviceCompleted(TransactionReference.Create("ADV-1", "ADVICE"), DateTime.UtcNow);
        return tx;
    }
}
