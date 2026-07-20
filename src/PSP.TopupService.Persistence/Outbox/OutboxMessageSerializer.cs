using System.Text.Json;
using System.Text.Json.Serialization;
using PSP.TopupService.Domain.Topups.Enums;
using PSP.TopupService.Domain.Topups.ValueObjects;

namespace PSP.TopupService.Persistence.Outbox;

/// <summary>
/// Serializes integration-event payloads to JSON for the outbox table. Keeping
/// the serializer in one place guarantees every payload carries the
/// <c>$type</c> discriminator and stable field names, regardless of consumer.
/// </summary>
public sealed class OutboxMessageSerializer
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false,
    };

    public string SerializeTopupCreated(Guid topupId, MobileNumber mobileNumber, Money amount) =>
        Serialize(new IntegrationEnvelope(
            "TopupCreated",
            new TopupCreatedPayload(topupId, mobileNumber.Value, amount.Value, amount.Currency)));

    public string SerializePaymentRequested(Guid topupId, Money amount, TransactionReference bankReference) =>
        Serialize(new IntegrationEnvelope(
            "PaymentRequested",
            new PaymentRequestedPayload(topupId, amount.Value, amount.Currency, bankReference.Value, bankReference.Source)));

    public string SerializeTopupCompleted(Guid topupId, TransactionReference mciReference) =>
        Serialize(new IntegrationEnvelope(
            "TopupCompleted",
            new TopupCompletedPayload(topupId, mciReference.Value, mciReference.Source)));

    public string SerializePaymentReversed(Guid topupId, TransactionReference reversalReference, TopupFailureReason reason) =>
        Serialize(new IntegrationEnvelope(
            "PaymentReversed",
            new PaymentReversedPayload(topupId, reversalReference.Value, reversalReference.Source, reason.ToString())));

    private static string Serialize(IntegrationEnvelope envelope) =>
        JsonSerializer.Serialize(envelope, Options);
}

/// <summary>
/// Envelope wrapping every integration event so consumers always know the
/// payload type and version without parsing the body.
/// </summary>
public sealed record IntegrationEnvelope
{
    public IntegrationEnvelope(string type, object payload)
    {
        Type = type;
        Payload = payload;
    }

    [JsonPropertyName("$type")]
    public string Type { get; init; }

    public object Payload { get; init; }

    public int Version => 1;

    public DateTime OccurredOnUtc => DateTime.UtcNow;
}

internal sealed record TopupCreatedPayload(Guid TopupId, string MobileNumber, decimal Amount, string Currency);

internal sealed record PaymentRequestedPayload(Guid TopupId, decimal Amount, string Currency, string BankReference, string BankSource);

internal sealed record TopupCompletedPayload(Guid TopupId, string MciReference, string MciSource);

internal sealed record PaymentReversedPayload(Guid TopupId, string ReversalReference, string ReversalSource, string Reason);
