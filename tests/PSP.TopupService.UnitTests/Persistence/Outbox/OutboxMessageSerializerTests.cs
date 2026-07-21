using System.Text.Json;
using PSP.TopupService.Domain.Topups.Enums;
using PSP.TopupService.Domain.Topups.ValueObjects;
using PSP.TopupService.Persistence.Outbox;

namespace PSP.TopupService.UnitTests.Persistence.Outbox;

/// <summary>
/// Verifies that <see cref="OutboxMessageSerializer"/> produces stable, typed
/// JSON envelopes so consumers can route by <c>$type</c> without parsing the
/// payload body. The wire contract is part of the public interface and must not
/// drift silently.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Layer", "Persistence")]
public class OutboxMessageSerializerTests
{
    private readonly OutboxMessageSerializer _serializer = new();

    [Fact]
    public void SerializeTopupCreated_Should_Produce_Envelope_With_Type_And_Version()
    {
        var correlationId = Guid.NewGuid();
        var payload = _serializer.SerializeTopupCreated(
            Guid.NewGuid(),
            MobileNumber.Create("09121234567"),
            Money.Create(50_000),
            correlationId);

        using var doc = JsonDocument.Parse(payload);
        doc.RootElement.GetProperty("$type").GetString().Should().Be("TopupCreated");
        doc.RootElement.GetProperty("version").GetInt32().Should().Be(1);
        doc.RootElement.GetProperty("correlationId").GetGuid().Should().Be(correlationId);
        doc.RootElement.GetProperty("payload").GetProperty("mobileNumber").GetString().Should().Be("09121234567");
        doc.RootElement.GetProperty("payload").GetProperty("amount").GetDecimal().Should().Be(50_000m);
        doc.RootElement.GetProperty("payload").GetProperty("currency").GetString().Should().Be("IRR");
    }

    [Fact]
    public void SerializePaymentRequested_Should_Carry_Bank_Reference()
    {
        var topupId = Guid.NewGuid();
        var bankRef = TransactionReference.Create("BANK-1", "BANK");

        var payload = _serializer.SerializePaymentRequested(topupId, Money.Create(20_000), bankRef, Guid.NewGuid());

        using var doc = JsonDocument.Parse(payload);
        doc.RootElement.GetProperty("$type").GetString().Should().Be("PaymentRequested");
        doc.RootElement.GetProperty("payload").GetProperty("topupId").GetGuid().Should().Be(topupId);
        doc.RootElement.GetProperty("payload").GetProperty("bankReference").GetString().Should().Be("BANK-1");
        doc.RootElement.GetProperty("payload").GetProperty("bankSource").GetString().Should().Be("BANK");
    }

    [Fact]
    public void SerializeTopupCompleted_Should_Carry_Mci_Reference()
    {
        var topupId = Guid.NewGuid();
        var mciRef = TransactionReference.Create("MCI-9", "MCI");

        var payload = _serializer.SerializeTopupCompleted(topupId, mciRef, Guid.NewGuid());

        using var doc = JsonDocument.Parse(payload);
        doc.RootElement.GetProperty("$type").GetString().Should().Be("TopupCompleted");
        doc.RootElement.GetProperty("payload").GetProperty("mciReference").GetString().Should().Be("MCI-9");
        doc.RootElement.GetProperty("payload").GetProperty("mciSource").GetString().Should().Be("MCI");
    }

    [Fact]
    public void SerializePaymentReversed_Should_Carry_Failure_Reason()
    {
        var topupId = Guid.NewGuid();
        var reversalRef = TransactionReference.Generate("REVERSAL");

        var payload = _serializer.SerializePaymentReversed(topupId, reversalRef, TopupFailureReason.TopupProviderError, Guid.NewGuid());

        using var doc = JsonDocument.Parse(payload);
        doc.RootElement.GetProperty("$type").GetString().Should().Be("PaymentReversed");
        doc.RootElement.GetProperty("payload").GetProperty("reason").GetString().Should().Be("TopupProviderError");
        doc.RootElement.GetProperty("payload").GetProperty("reversalSource").GetString().Should().Be("REVERSAL");
    }

    [Fact]
    public void Envelopes_Should_Always_Carry_OccurredOnUtc()
    {
        var payload = _serializer.SerializeTopupCreated(Guid.NewGuid(), MobileNumber.Create("09121234567"), Money.Create(10_000), Guid.NewGuid());

        using var doc = JsonDocument.Parse(payload);
        var occurred = doc.RootElement.GetProperty("occurredOnUtc").GetDateTime();
        occurred.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void Envelopes_Should_Use_CamelCase_Property_Naming()
    {
        var payload = _serializer.SerializeTopupCompleted(Guid.NewGuid(), TransactionReference.Create("X", "MCI"), Guid.NewGuid());

        payload.Should().Contain("\"$type\"");
        payload.Should().Contain("\"topupId\"");
        payload.Should().NotContain("TopupId"); // no PascalCase leaked
    }
}
