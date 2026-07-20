using PSP.TopupService.Domain.Topups.ValueObjects;

namespace PSP.TopupService.UnitTests.Domain.Topups;

/// <summary>
/// Verifies <see cref="MobileNumber"/> normalisation, validation and equality.
/// Mobile numbers are user input and the primary key in the operator's system,
/// so canonicalisation must be deterministic.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Layer", "Domain")]
public class MobileNumberTests
{
    [Theory]
    [InlineData("09121234567")]
    [InlineData(" 09121234567 ")]
    [InlineData("0912-123-4567")]
    [InlineData("0912 123 4567")]
    [InlineData("+989121234567")]
    [InlineData("00989121234567")]
    [InlineData("989121234567")]
    public void Create_Should_Normalize_To_Canonical_Form(string input)
    {
        var number = MobileNumber.Create(input);

        number.Value.Should().Be("09121234567");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("12345")]
    [InlineData("08121234567")]   // must start with 09
    [InlineData("0912123456")]     // too short
    [InlineData("091212345678")]   // too long
    [InlineData("abc")]
    public void Create_Should_Throw_For_Invalid_Input(string input)
    {
        var act = () => MobileNumber.Create(input);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void TryCreate_Should_Return_Null_For_Invalid()
    {
        MobileNumber.TryCreate("nope").Should().BeNull();
    }

    [Fact]
    public void TryCreate_Should_Return_Instance_For_Valid()
    {
        var number = MobileNumber.TryCreate("09121234567");
        number.Should().NotBeNull();
        number!.Value.Should().Be("09121234567");
    }

    [Fact]
    public void ToInternational_Should_Return_Plus98_Form()
    {
        var number = MobileNumber.Create("09121234567");
        number.ToInternational().Should().Be("+989121234567");
    }

    [Fact]
    public void Equality_Should_Be_Based_On_Canonical_Value()
    {
        var a = MobileNumber.Create("+989121234567");
        var b = MobileNumber.Create("0912-123-4567");

        a.Should().Be(b);
        (a == b).Should().BeTrue();
        a.GetHashCode().Should().Be(b.GetHashCode());
    }
}
