using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using PSP.TopupService.Application.Common.Abstractions;
using PSP.TopupService.Application.Common.Outbox;
using PSP.TopupService.Application.Topups.Commands.PerformAdvice;
using PSP.TopupService.Domain.Topups;
using PSP.TopupService.Domain.Topups.Enums;
using PSP.TopupService.Domain.Topups.ValueObjects;
using PSP.TopupService.SharedKernel.Exceptions;

namespace PSP.TopupService.UnitTests.Application.Topups.PerformAdvice;

/// <summary>
/// Verifies <see cref="PerformAdviceCommandHandler"/> under the event-driven
/// flow: the handler dispatches an AdviceRequestedEvent to the Payment service
/// via the outbox and records the attempt on the aggregate. No HTTP call.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Layer", "Application")]
public class PerformAdviceCommandHandlerTests
{
    private readonly Mock<ITopupRepository> _repository = new();
    private readonly Mock<IOutboxWriter> _outbox = new();
    private readonly AdviceOptions _options = new() { MaxRetries = 3, RetryDelay = TimeSpan.FromMilliseconds(10), BackoffMultiplier = 1.0 };

    [Fact]
    public async Task Handle_Below_MaxRetries_Should_Dispatch_AdviceRequested_Via_Outbox()
    {
        var tx = AdvicePendingTransaction();
        _repository.Setup(r => r.GetByIdForUpdateAsync(tx.Id, It.IsAny<CancellationToken>())).ReturnsAsync(tx);

        var handler = BuildHandler();
        var result = await handler.Handle(Command(tx.Id, adviceAttempt: 0), CancellationToken.None);

        result.FinalStatus.Should().Be(nameof(TopupStatus.AdvicePending));
        tx.Status.Should().Be(TopupStatus.AdvicePending);
        tx.AdviceRetryCount.Should().Be(1);
        tx.FailureReason.Should().Be(TopupFailureReason.None); // not terminal yet
        _outbox.Verify(o => o.EnqueueAdviceRequestedAsync(
            tx.Id, tx.BankReference!, tx.Amount, tx.CorrelationId, It.IsAny<DateTime?>(), 0, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_At_MaxRetries_Should_Flag_AdviceFailed_Without_AutoReversal()
    {
        var tx = AdvicePendingTransaction();
        // Pre-burn retries up to MaxRetries - 1 so this attempt crosses the threshold.
        for (var i = 0; i < _options.MaxRetries - 1; i++)
        {
            tx.MarkAdviceAttemptFailed("previous", _options.MaxRetries);
        }

        _repository.Setup(r => r.GetByIdForUpdateAsync(tx.Id, It.IsAny<CancellationToken>())).ReturnsAsync(tx);

        var handler = BuildHandler();
        var result = await handler.Handle(Command(tx.Id, adviceAttempt: _options.MaxRetries - 1), CancellationToken.None);

        result.FinalStatus.Should().Be(nameof(TopupStatus.AdvicePending));
        tx.FailureReason.Should().Be(TopupFailureReason.AdviceFailed);
        tx.FailureMessage.Should().Contain("Bank advice failed terminally");
        // CRITICAL: the topup succeeded, so we MUST NOT auto-reverse it.
        _outbox.Verify(o => o.EnqueuePaymentReversedAsync(It.IsAny<Guid>(), It.IsAny<TransactionReference>(), It.IsAny<TopupFailureReason>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        _outbox.Verify(o => o.EnqueueReverseRequestedAsync(It.IsAny<Guid>(), It.IsAny<TransactionReference>(), It.IsAny<Money>(), It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        _outbox.Verify(o => o.EnqueueAdviceRequestedAsync(It.IsAny<Guid>(), It.IsAny<TransactionReference>(), It.IsAny<Money>(), It.IsAny<Guid>(), It.IsAny<DateTime?>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_Should_Replay_When_Aggregate_Already_Terminal()
    {
        var tx = CompletedTopup();
        _repository.Setup(r => r.GetByIdForUpdateAsync(tx.Id, It.IsAny<CancellationToken>())).ReturnsAsync(tx);

        var handler = BuildHandler();
        var result = await handler.Handle(Command(tx.Id), CancellationToken.None);

        result.WasIdempotentReplay.Should().BeTrue();
        _outbox.Verify(o => o.EnqueueAdviceRequestedAsync(It.IsAny<Guid>(), It.IsAny<TransactionReference>(), It.IsAny<Money>(), It.IsAny<Guid>(), It.IsAny<DateTime?>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
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

    private PerformAdviceCommandHandler BuildHandler() =>
        new(_repository.Object, _outbox.Object, Options.Create(_options), NullLogger<PerformAdviceCommandHandler>.Instance);

    private static PerformAdviceCommand Command(Guid topupId, int adviceAttempt = 0) =>
        new() { TopupId = topupId, MessageId = Guid.NewGuid(), AdviceAttempt = adviceAttempt };

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
