using PSP.TopupService.SharedKernel.Guards;
using PSP.TopupService.SharedKernel.ValueObjects;

namespace PSP.TopupService.Domain.Topups.ValueObjects;

/// <summary>
/// A monetary amount with an ISO-4217 currency code. The Topup service operates
/// in Iranian Rial (<c>IRR</c>) by default; the currency is carried explicitly
/// so future multi-currency support requires no schema change.
/// </summary>
public sealed class Money : ValueObject
{
    /// <summary>
    /// Minimum allowed topup amount (1,000 IRR = 100 Toman). Below this the
    /// operator rejects the topup, so we fail fast in the domain.
    /// </summary>
    public const decimal MinimumTopupAmount = 1_000m;

    /// <summary>
    /// Maximum allowed single topup amount (10,000,000 IRR = 1,000,000 Toman).
    /// Above this, transactions are routed to a different product; enforced here.
    /// </summary>
    public const decimal MaximumTopupAmount = 10_000_000m;

    private Money(decimal value, string currency)
    {
        Value = value;
        Currency = currency;
    }

    public decimal Value { get; }

    /// <summary>ISO-4217 currency code, upper-case (e.g. <c>IRR</c>).</summary>
    public string Currency { get; }

    public static Money Create(decimal value, string currency = "IRR")
    {
        Guard.NotNullOrWhiteSpace(currency, nameof(currency));

        if (value <= 0)
        {
            throw new ArgumentException("Amount must be greater than zero.", nameof(value));
        }

        if (value < MinimumTopupAmount || value > MaximumTopupAmount)
        {
            throw new ArgumentOutOfRangeException(
                nameof(value),
                value,
                $"Amount must be between {MinimumTopupAmount} and {MaximumTopupAmount} {currency}.");
        }

        return new Money(value, currency.ToUpperInvariant());
    }

    public static Money CreateForTopup(decimal value, string currency = "IRR")
    {
        // Domain-level validation specific to topup amounts.
        return Create(value, currency);
    }

    public bool IsWithinTopupRange() =>
        Value >= MinimumTopupAmount && Value <= MaximumTopupAmount;

    public override string ToString() => $"{Value:N0} {Currency}";

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
        yield return Currency;
    }
}
