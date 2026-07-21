using FluentValidation.TestHelper;
using PSP.TopupService.Application.Topups.Commands;

namespace PSP.TopupService.UnitTests.Application.Topups.Commands;

/// <summary>
/// Verifies the input-shape validation performed by
/// <see cref="CreateTopupCommandValidator"/>. Semantic validation lives in the
/// domain value objects and is exercised in the domain test suite.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Layer", "Application")]
public class CreateTopupCommandValidatorTests
{
    private readonly CreateTopupCommandValidator _validator = new();

    [Fact]
    public async Task Should_Pass_For_Valid_Command()
    {
        var command = new CreateTopupCommand
        {
            MobileNumber = "09121234567",
            Amount = 50_000,
        };

        var result = await _validator.TestValidateAsync(command);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Should_Fail_When_MobileNumber_Empty()
    {
        var command = new CreateTopupCommand { MobileNumber = string.Empty, Amount = 50_000 };

        var result = await _validator.TestValidateAsync(command);

        result.IsValid.Should().BeFalse();
        result.ShouldHaveValidationErrorFor(x => x.MobileNumber);
    }

    [Theory]
    [InlineData("12345")]        // too short
    [InlineData("08121234567")]  // wrong prefix
    [InlineData("0912123456")]   // 10 digits
    [InlineData("abc-def-ghij")]
    public async Task Should_Fail_For_Malformed_MobileNumber(string mobile)
    {
        var command = new CreateTopupCommand { MobileNumber = mobile, Amount = 50_000 };

        var result = await _validator.TestValidateAsync(command);

        result.ShouldHaveValidationErrorFor(x => x.MobileNumber);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-100)]
    public async Task Should_Fail_When_Amount_Not_Positive(decimal amount)
    {
        var command = new CreateTopupCommand { MobileNumber = "09121234567", Amount = amount };

        var result = await _validator.TestValidateAsync(command);

        result.ShouldHaveValidationErrorFor(x => x.Amount);
    }

    [Theory]
    [InlineData(999)]            // below minimum
    [InlineData(10_000_001)]     // above maximum
    public async Task Should_Fail_When_Amount_Outside_Topup_Range(decimal amount)
    {
        var command = new CreateTopupCommand { MobileNumber = "09121234567", Amount = amount };

        var result = await _validator.TestValidateAsync(command);

        result.ShouldHaveValidationErrorFor(x => x.Amount);
    }

    [Fact]
    public async Task Should_Fail_When_IdempotencyKey_Too_Long()
    {
        var command = new CreateTopupCommand
        {
            MobileNumber = "09121234567",
            Amount = 50_000,
            IdempotencyKey = new string('x', 257),
        };

        var result = await _validator.TestValidateAsync(command);

        result.ShouldHaveValidationErrorFor(x => x.IdempotencyKey);
    }
}
