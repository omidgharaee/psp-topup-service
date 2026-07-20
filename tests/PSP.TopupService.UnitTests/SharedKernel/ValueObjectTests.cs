using PSP.TopupService.SharedKernel.ValueObjects;

namespace PSP.TopupService.UnitTests.SharedKernel;

/// <summary>
/// Verifies value-object equality semantics — two VOs with the same components
/// are equal and produce the same hash code.
/// </summary>
public class ValueObjectTests
{
    [Fact]
    public void Equals_Should_Return_True_For_Same_Components()
    {
        var a = new Money(100, "IRR");
        var b = new Money(100, "IRR");

        a.Equals(b).Should().BeTrue();
        (a == b).Should().BeTrue();
    }

    [Fact]
    public void Equals_Should_Return_False_For_Different_Components()
    {
        var a = new Money(100, "IRR");
        var b = new Money(200, "IRR");
        var c = new Money(100, "USD");

        a.Equals(b).Should().BeFalse();
        a.Equals(c).Should().BeFalse();
    }

    [Fact]
    public void GetHashCode_Should_Match_For_Equal_Objects()
    {
        var a = new Money(100, "IRR");
        var b = new Money(100, "IRR");

        a.GetHashCode().Should().Be(b.GetHashCode());
    }

    [Fact]
    public void Equals_Should_Return_False_When_Comparing_To_Null_Or_Other_Type()
    {
        var a = new Money(100, "IRR");

#pragma warning disable CA1508 // we deliberately pass null to exercise the null branch
        a.Equals((object?)null).Should().BeFalse();
#pragma warning restore CA1508
        object other = "not a money";
        a.Equals(other).Should().BeFalse();
    }

    /// <summary>A small value object used purely for testing equality semantics.</summary>
    private sealed class Money : ValueObject
    {
        public Money(decimal amount, string currency)
        {
            Amount = amount;
            Currency = currency;
        }

        public decimal Amount { get; }
        public string Currency { get; }

        protected override IEnumerable<object?> GetEqualityComponents()
        {
            yield return Amount;
            yield return Currency;
        }
    }
}
