using PSP.TopupService.Domain.Topups.ValueObjects;

namespace PSP.TopupService.UnitTests.Domain.Topups;

/// <summary>
/// Verifies <see cref="Money"/> validation, currency handling and equality.
/// Topup amounts are financial — bounds must be enforced at the domain edge.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Layer", "Domain")]
public class MoneyTests
{
    [Fact]
    public void Create_Should_Persist_Value_And_Normalised_Currency()
    {
        var money = Money.Create(50_000, "irr");

        money.Value.Should().Be(50_000);
        money.Currency.Should().Be("IRR");
    }

    [Fact]
    public void Create_Should_Default_Currency_To_IRR()
    {
        var money = Money.Create(10_000);

        money.Currency.Should().Be("IRR");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-50_000)]
    public void Create_Should_Throw_For_Non_Positive(decimal amount)
    {
        var act = () => Money.Create(amount);
        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData(999)]              // below minimum
    [InlineData(10_000_001)]       // above maximum
    public void Create_Should_Throw_Outside_Topup_Range(decimal amount)
    {
        var act = () => Money.Create(amount);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Theory]
    [InlineData(1_000)]              // Money.MinimumTopupAmount
    [InlineData(10_000_000)]         // Money.MaximumTopupAmount
    public void Create_Should_Allow_Range_Boundaries(decimal amount)
    {
        var money = Money.Create(amount);
        money.Value.Should().Be(amount);
        money.Value.Should().BeGreaterThanOrEqualTo(Money.MinimumTopupAmount);
        money.Value.Should().BeLessThanOrEqualTo(Money.MaximumTopupAmount);
    }

    [Fact]
    public void Equality_Should_Compare_Value_And_Currency()
    {
        var a = Money.Create(50_000, "IRR");
        var b = Money.Create(50_000, "IRR");

        a.Should().Be(b);
        (a == b).Should().BeTrue();
    }

    [Fact]
    public void Different_Currencies_Should_Not_Be_Equal()
    {
        var irr = Money.Create(50_000, "IRR");
        var usd = Money.Create(50_000, "USD");

        irr.Should().NotBe(usd);
    }
}
