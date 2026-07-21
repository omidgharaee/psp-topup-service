using PSP.TopupService.Contracts.Events;
using PSP.TopupService.Domain.Topups.ValueObjects;
using PSP.TopupService.Infrastructure.Messaging.Publishers;
using PSP.TopupService.Persistence.Outbox;

namespace PSP.TopupService.UnitTests.Infrastructure.Messaging;

/// <summary>
/// Verifies <see cref="IntegrationEventMapper"/>: round-trip from a serialized
/// outbox envelope back to the typed event. The tests use the real
/// <see cref="OutboxMessageSerializer"/> so the wire contract is exercised
/// end-to-end. Wrong shape or unknown types must return null so the publisher
/// treats them as failures.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Layer", "Infrastructure")]
public class IntegrationEventMapperTests
{
    private readonly IntegrationEventMapper _mapper = new();
    private readonly OutboxMessageSerializer _serializer = new();

    [Fact]
    public void Map_Should_RoundTrip_TopupCreatedEvent()
    {
        var topupId = Guid.NewGuid();
        var correlationId = Guid.NewGuid();
        var payload = _serializer.SerializeTopupCreated(topupId, MobileNumber.Create("09121234567"), Money.Create(50_000), correlationId);

        var result = _mapper.Map("TopupCreated", payload);

        result.Should().NotBeNull().And.BeOfType<TopupCreatedEvent>();
        var created = (TopupCreatedEvent)result!;
        created.TopupId.Should().Be(topupId);
        created.MobileNumber.Should().Be("09121234567");
        created.Amount.Should().Be(50_000m);
        created.Currency.Should().Be("IRR");
        created.CorrelationId.Should().Be(correlationId);
    }

    [Fact]
    public void Map_Should_RoundTrip_PaymentRequestedEvent()
    {
        var topupId = Guid.NewGuid();
        var correlationId = Guid.NewGuid();
        var bankRef = TransactionReference.Create("B-1", "BANK");
        var payload = _serializer.SerializePaymentRequested(topupId, Money.Create(10_000), bankRef, correlationId);

        var result = _mapper.Map("PaymentRequested", payload);

        result.Should().NotBeNull().And.BeOfType<PaymentRequestedEvent>();
        var requested = (PaymentRequestedEvent)result!;
        requested.TopupId.Should().Be(topupId);
        requested.BankReference.Should().Be("B-1");
        requested.BankSource.Should().Be("BANK");
        requested.CorrelationId.Should().Be(correlationId);
    }

    [Fact]
    public void Map_Should_RoundTrip_TopupCompletedEvent()
    {
        var topupId = Guid.NewGuid();
        var correlationId = Guid.NewGuid();
        var payload = _serializer.SerializeTopupCompleted(topupId, TransactionReference.Create("MCI-9", "MCI"), correlationId);

        var result = _mapper.Map("TopupCompleted", payload);

        result.Should().NotBeNull().And.BeOfType<TopupCompletedEvent>();
        var completed = (TopupCompletedEvent)result!;
        completed.TopupId.Should().Be(topupId);
        completed.CorrelationId.Should().Be(correlationId);
    }

    [Fact]
    public void Map_Should_RoundTrip_PaymentReversedEvent()
    {
        var topupId = Guid.NewGuid();
        var correlationId = Guid.NewGuid();
        var payload = _serializer.SerializePaymentReversed(topupId, TransactionReference.Generate("REVERSAL"), PSP.TopupService.Domain.Topups.Enums.TopupFailureReason.TopupProviderError, correlationId);

        var result = _mapper.Map("PaymentReversed", payload);

        result.Should().NotBeNull().And.BeOfType<PaymentReversedEvent>();
        var reversed = (PaymentReversedEvent)result!;
        reversed.TopupId.Should().Be(topupId);
        reversed.Reason.Should().Be("TopupProviderError");
        reversed.CorrelationId.Should().Be(correlationId);
    }

    [Fact]
    public void Map_Should_Return_Null_For_Unknown_Type()
    {
        var payload = """{"$type":"Foo","payload":{}}""";

        _mapper.Map("Foo", payload).Should().BeNull();
    }

    [Fact]
    public void Map_Should_Return_Null_For_Malformed_Json()
    {
        _mapper.Map("TopupCreated", "{not-json").Should().BeNull();
    }

    [Fact]
    public void Map_Should_Return_Null_When_Envelope_Lacks_Payload_Property()
    {
        var payload = """{"$type":"TopupCreated"}""";

        _mapper.Map("TopupCreated", payload).Should().BeNull();
    }
}
