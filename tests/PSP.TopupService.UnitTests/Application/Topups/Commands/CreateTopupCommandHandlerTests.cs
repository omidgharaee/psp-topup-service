using MediatR;
using Microsoft.Extensions.Logging;
using Moq;
using PSP.TopupService.Application.Common.Abstractions;
using PSP.TopupService.Application.Common.Behaviors;
using PSP.TopupService.Application.Common.Context;
using PSP.TopupService.Application.Common.Outbox;
using PSP.TopupService.Application.Topups.Commands;
using PSP.TopupService.Domain.Topups;
using PSP.TopupService.Domain.Topups.ValueObjects;

namespace PSP.TopupService.UnitTests.Application.Topups.Commands;

/// <summary>
/// Verifies <see cref="CreateTopupCommandHandler"/>: idempotency replay,
/// successful creation and outbox enqueueing. Repository/UoW are mocked.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Layer", "Application")]
public class CreateTopupCommandHandlerTests
{
    private readonly Mock<ITopupRepository> _repository = new();
    private readonly Mock<IOutboxWriter> _outbox = new();
    private readonly Mock<ICorrelationContext> _correlation = new();

    [Fact]
    public async Task Handle_Should_Create_Transaction_And_Enqueue_Outbox()
    {
        _correlation.SetupGet(c => c.CorrelationId).Returns(Guid.NewGuid());
        _correlation.SetupGet(c => c.Actor).Returns("actor-1");
        _repository.Setup(r => r.AddAsync(It.IsAny<TopupTransaction>(), It.IsAny<CancellationToken>()))
            .Callback<TopupTransaction, CancellationToken>((t, _) => t.GetType().Name.Contains("Topup"))
            .ReturnsAsync((TopupTransaction t, CancellationToken _) => t);
        var handler = new CreateTopupCommandHandler(_repository.Object, _outbox.Object, _correlation.Object);

        var result = await handler.Handle(new CreateTopupCommand
        {
            MobileNumber = "09121234567",
            Amount = 50_000,
            Actor = "actor-1",
            RemoteIp = "1.2.3.4",
        }, CancellationToken.None);

        result.IsIdempotentReplay.Should().BeFalse();
        result.Response.TransactionId.Should().NotBeEmpty();
        result.Response.Status.Should().Be("Pending");
        result.Response.MobileNumber.Should().Be("09121234567");
        result.Response.Amount.Should().Be(50_000);

        _repository.Verify(r => r.AddAsync(It.IsAny<TopupTransaction>(), It.IsAny<CancellationToken>()), Times.Once);
        _outbox.Verify(o => o.EnqueueTopupCreatedAsync(
            It.IsAny<Guid>(),
            It.IsAny<MobileNumber>(),
            It.IsAny<Money>(),
            It.IsAny<Guid>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_Should_Replay_When_Idempotency_Key_Already_Processed()
    {
        var existing = TopupTransaction.Create(
            MobileNumber.Create("09121234567"),
            Money.Create(50_000),
            Guid.NewGuid(),
            "actor-1",
            "key-1");

        _repository.Setup(r => r.GetByIdempotencyKeyAsync("key-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        var handler = new CreateTopupCommandHandler(_repository.Object, _outbox.Object, _correlation.Object);

        var result = await handler.Handle(new CreateTopupCommand
        {
            MobileNumber = "09121234567",
            Amount = 50_000,
            IdempotencyKey = "key-1",
        }, CancellationToken.None);

        result.IsIdempotentReplay.Should().BeTrue();
        result.Response.TransactionId.Should().Be(existing.Id);

        _repository.Verify(r => r.AddAsync(It.IsAny<TopupTransaction>(), It.IsAny<CancellationToken>()), Times.Never);
        _outbox.Verify(o => o.EnqueueTopupCreatedAsync(
            It.IsAny<Guid>(),
            It.IsAny<MobileNumber>(),
            It.IsAny<Money>(),
            It.IsAny<Guid>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_Should_Throw_For_Invalid_Amount()
    {
        var handler = new CreateTopupCommandHandler(_repository.Object, _outbox.Object, _correlation.Object);

        var act = () => handler.Handle(new CreateTopupCommand
        {
            MobileNumber = "09121234567",
            Amount = 0,
        }, CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task Handle_Should_Propagate_CorrelationId_When_Set()
    {
        var correlationId = Guid.NewGuid();
        _correlation.SetupGet(c => c.CorrelationId).Returns(correlationId);
        _correlation.SetupGet(c => c.Actor).Returns((string?)null);
        _repository.Setup(r => r.AddAsync(It.IsAny<TopupTransaction>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((TopupTransaction t, CancellationToken _) => t);

        var handler = new CreateTopupCommandHandler(_repository.Object, _outbox.Object, _correlation.Object);

        var result = await handler.Handle(new CreateTopupCommand
        {
            MobileNumber = "09121234567",
            Amount = 50_000,
        }, CancellationToken.None);

        result.Response.TransactionId.Should().NotBeEmpty();

        _outbox.Verify(o => o.EnqueueTopupCreatedAsync(
            It.IsAny<Guid>(),
            It.IsAny<MobileNumber>(),
            It.IsAny<Money>(),
            correlationId, // must match the ambient correlation id
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
