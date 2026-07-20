using System.Text.Json;
using PSP.TopupService.Contracts;
using PSP.TopupService.Contracts.Events;
using PSP.TopupService.Infrastructure.Messaging.Abstractions;

namespace PSP.TopupService.Infrastructure.Messaging.Publishers;

/// <summary>
/// Default <see cref="IIntegrationEventMapper"/>. Reads the <c>$type</c>
/// discriminator from the outbox envelope and constructs the matching
/// <see cref="IIntegrationEvent"/> from the payload fields plus the envelope
/// correlation id / occurred-on. Unknown types return null, which the publisher
/// treats as a hard failure.
/// </summary>
public sealed class IntegrationEventMapper : IIntegrationEventMapper
{
    public IIntegrationEvent? Map(string eventType, string payload)
    {
        try
        {
            using var doc = JsonDocument.Parse(payload);
            if (!doc.RootElement.TryGetProperty("payload", out var payloadElement))
            {
                return null;
            }

            var correlationId = TryGetGuid(doc.RootElement, "correlationId") ?? Guid.Empty;
            var occurredOnUtc = TryGetDateTime(doc.RootElement, "occurredOnUtc") ?? DateTime.UtcNow;

            // Accept both short ("TopupCreated") and full ("TopupCreatedEvent") type names
            // so the wire contract is forgiving of producer conventions.
            return eventType switch
            {
                nameof(TopupCreatedEvent) or "TopupCreated" => BuildTopupCreated(payloadElement, correlationId, occurredOnUtc),
                nameof(PaymentRequestedEvent) or "PaymentRequested" => BuildPaymentRequested(payloadElement, correlationId, occurredOnUtc),
                nameof(TopupCompletedEvent) or "TopupCompleted" => BuildTopupCompleted(payloadElement, correlationId, occurredOnUtc),
                nameof(PaymentReversedEvent) or "PaymentReversed" => BuildPaymentReversed(payloadElement, correlationId, occurredOnUtc),
                nameof(AdviceRequestedEvent) or "AdviceRequested" => BuildAdviceRequested(payloadElement, correlationId, occurredOnUtc),
                _ => null,
            };
        }
        catch (Exception)
        {
            // Any failure (JSON parse, missing field, type mismatch) is treated
            // as a non-mappable payload; the publisher will mark it failed.
            return null;
        }
    }

    private static TopupCreatedEvent BuildTopupCreated(JsonElement p, Guid correlationId, DateTime occurredOnUtc) =>
        new(
            topupId: TryGetGuid(p, "topupId") ?? Guid.Empty,
            mobileNumber: TryGetString(p, "mobileNumber") ?? string.Empty,
            amount: TryGetDecimal(p, "amount") ?? 0m,
            currency: TryGetString(p, "currency") ?? "IRR",
            correlationId: correlationId,
            occurredOnUtc: occurredOnUtc);

    private static PaymentRequestedEvent BuildPaymentRequested(JsonElement p, Guid correlationId, DateTime occurredOnUtc) =>
        new(
            topupId: TryGetGuid(p, "topupId") ?? Guid.Empty,
            amount: TryGetDecimal(p, "amount") ?? 0m,
            currency: TryGetString(p, "currency") ?? "IRR",
            bankReference: TryGetString(p, "bankReference") ?? string.Empty,
            bankSource: TryGetString(p, "bankSource") ?? string.Empty,
            correlationId: correlationId,
            occurredOnUtc: occurredOnUtc);

    private static TopupCompletedEvent BuildTopupCompleted(JsonElement p, Guid correlationId, DateTime occurredOnUtc) =>
        new(
            topupId: TryGetGuid(p, "topupId") ?? Guid.Empty,
            mciReference: TryGetString(p, "mciReference") ?? string.Empty,
            mciSource: TryGetString(p, "mciSource") ?? string.Empty,
            correlationId: correlationId,
            occurredOnUtc: occurredOnUtc);

    private static PaymentReversedEvent BuildPaymentReversed(JsonElement p, Guid correlationId, DateTime occurredOnUtc) =>
        new(
            topupId: TryGetGuid(p, "topupId") ?? Guid.Empty,
            reversalReference: TryGetString(p, "reversalReference") ?? string.Empty,
            reversalSource: TryGetString(p, "reversalSource") ?? string.Empty,
            reason: TryGetString(p, "reason") ?? string.Empty,
            correlationId: correlationId,
            occurredOnUtc: occurredOnUtc);

    private static AdviceRequestedEvent BuildAdviceRequested(JsonElement p, Guid correlationId, DateTime occurredOnUtc) =>
        new(
            topupId: TryGetGuid(p, "topupId") ?? Guid.Empty,
            originalReference: TryGetString(p, "originalReference") ?? string.Empty,
            originalSource: TryGetString(p, "originalSource") ?? string.Empty,
            amount: TryGetDecimal(p, "amount") ?? 0m,
            currency: TryGetString(p, "currency") ?? "IRR",
            adviceAttempt: TryGetInt(p, "adviceAttempt") ?? 0,
            correlationId: correlationId,
            occurredOnUtc: occurredOnUtc);

    private static Guid? TryGetGuid(JsonElement element, string propertyName)
    {
        if (element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String)
        {
            return Guid.TryParse(value.GetString(), out var g) ? g : null;
        }

        return null;
    }

    private static string? TryGetString(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
    }

    private static decimal? TryGetDecimal(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.Number
            ? value.GetDecimal()
            : null;
    }

    private static int? TryGetInt(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.Number
            ? value.GetInt32()
            : null;
    }

    private static DateTime? TryGetDateTime(JsonElement element, string propertyName)
    {
        if (element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String)
        {
            return DateTime.TryParse(value.GetString(), out var dt) ? dt : null;
        }

        return null;
    }
}
