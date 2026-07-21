using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using PSP.TopupService.Application.Common.Abstractions;
using PSP.TopupService.Application.Common.Outbox;
using PSP.TopupService.Application.Topups.Abstractions;
using PSP.TopupService.Application.Topups.Commands.PerformTopup;
using PSP.TopupService.Domain.Topups;
using PSP.TopupService.Domain.Topups.Enums;
using PSP.TopupService.Domain.Topups.ValueObjects;
using PSP.TopupService.SharedKernel.Exceptions;

namespace PSP.TopupService.UnitTests.Application.Topups.PerformTopup;

/// <summary>
/// Verifies <see cref="PerformTopupCommandHandler"/> under the event-driven
/// flow: MCI success -> AdvicePending + outbox AdviceRequested, MCI failure ->
/// ReverseRecord initiated + outbox ReverseRequested. No direct Bank HTTP call.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Layer", "Application")]
public class PerformTopupCommandHandlerTests
{
    private readonly Mock<ITopupRepository> _repository = new();
    private readonly Mock<IHamrahAvalClient> _mci = new();
    private readonly Mock<IOutboxWriter> _outbox = new();

    [Fact]
    public async Task Handle_MciSuccess_Should_Move_To_AdvicePending_And_Enqueue_Advice()
    {
        var tx = PaidTransaction();
        var mciRef = TransactionReference.Create("MCI-OK", "MCI");
        _repository.Setup(r => r.GetByIdForUpdateAsync(tx.Id, It.IsAny<CancellationToken>())).ReturnsAsync(tx);
        _mci.Setup(c => c.TopupAsync(tx.Id, tx.MobileNumber, tx.Amount, tx.CorrelationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HamrahAvalTopupResult { ProviderReference = mciRef });

        var handler = BuildHandler();
        var result = await handler.Handle(Command(tx.Id), CancellationToken.None);

        result.WasIdempotentReplay.Should().BeFalse();
        result.FinalStatus.Should().Be(nameof(TopupStatus.AdvicePending));
        tx.Status.Should().Be(TopupStatus.AdvicePending);
        tx.MciReference.Should().Be(mciRef);
        tx.IsTerminal.Should().BeFalse();
        _outbox.Verify(o => o.EnqueueAdviceRequestedAsync(tx.Id, tx.BankReference!, tx.Amount, tx.CorrelationId, It.IsAny<DateTime?>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Once);
        _outbox.Verify(o => o.EnqueueReverseRequestedAsync(It.IsAny<Guid>(), It.IsAny<TransactionReference>(), It.IsAny<Money>(), It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_MciFailure_Should_Initiate_Reversal_And_Enqueue_ReverseRequested()
    {
        var tx = PaidTransaction();
        _repository.Setup(r => r.GetByIdForUpdateAsync(tx.Id, It.IsAny<CancellationToken>())).ReturnsAsync(tx);
        _mci.Setup(c => c.TopupAsync(tx.Id, tx.MobileNumber, tx.Amount, tx.CorrelationId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new PSP.TopupService.Application.Topups.Clients.HamrahAvalException(tx.Id, "all retries failed"));

        var handler = BuildHandler();
        var result = await handler.Handle(Command(tx.Id), CancellationToken.None);

        // The aggregate stays in TopupInProgress with an Initiated reversal
        // awaiting the asynchronous PaymentReversedEvent from the Payment service.
        result.FinalStatus.Should().Be(nameof(TopupStatus.TopupInProgress));
        tx.Status.Should().Be(TopupStatus.TopupInProgress);
        tx.FailureReason.Should().Be(TopupFailureReason.TopupProviderError);
        tx.Reverse.Should().NotBeNull();
        tx.Reverse!.Status.Should().Be(ReverseStatus.Initiated);
        _outbox.Verify(o => o.EnqueueReverseRequestedAsync(
            tx.Id, tx.BankReference!, tx.Amount, TopupFailureReason.TopupProviderError.ToString(), tx.CorrelationId,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_Should_Replay_When_Aggregate_Already_Terminal()
    {
        var tx = CompletedTopup();
        _repository.Setup(r => r.GetByIdForUpdateAsync(tx.Id, It.IsAny<CancellationToken>())).ReturnsAsync(tx);

        var handler = BuildHandler();
        var result = await handler.Handle(Command(tx.Id), CancellationToken.None);

        result.WasIdempotentReplay.Should().BeTrue();
        _mci.Verify(c => c.TopupAsync(It.IsAny<Guid>(), It.IsAny<MobileNumber>(), It.IsAny<Money>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_Should_Throw_NotFound_When_Aggregate_Missing()
    {
        _repository.Setup(r => r.GetByIdForUpdateAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((TopupTransaction?)null);

        var handler = BuildHandler();
        var act = () => handler.Handle(Command(Guid.NewGuid()), CancellationToken.None);
        await act.Should().ThrowAsync<NotFoundException>();
    }

    private PerformTopupCommandHandler BuildHandler() =>
        new(_repository.Object, _mci.Object, _outbox.Object, NullLogger<PerformTopupCommandHandler>.Instance);

    private static PerformTopupCommand Command(Guid topupId) => new() { TopupId = topupId, MessageId = Guid.NewGuid() };

    private static TopupTransaction PaidTransaction()
    {
        var tx = TopupTransaction.Create(MobileNumber.Create("09121234567"), Money.Create(50_000), Guid.NewGuid(), "actor");
        tx.MarkPaymentInitiated(TransactionReference.Generate("BANK"));
        tx.MarkPaymentCompleted(tx.BankReference!, DateTime.UtcNow);
        return tx;
    }

    private static TopupTransaction CompletedTopup()
    {
        var tx = PaidTransaction();
        var attempt = tx.StartTopupAttempt().Value!;
        attempt.MarkSucceeded("MCI-OK");
        tx.MarkTopupSucceeded(TransactionReference.Create("MCI-OK", "MCI"), DateTime.UtcNow);
        tx.MarkAdviceCompleted(TransactionReference.Create("ADV-1", "ADVICE"), DateTime.UtcNow);
        return tx;
    }
}
