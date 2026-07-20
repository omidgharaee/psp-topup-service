using PSP.TopupService.SharedKernel.Entities;
using PSP.TopupService.SharedKernel.Results;

namespace PSP.TopupService.UnitTests.SharedKernel;

/// <summary>
/// Verifies the Result pattern: success/failure invariants, chaining, mapping
/// and the typed <see cref="Result{TValue}"/> API.
/// </summary>
public class ResultTests
{
    [Fact]
    public void Success_Should_HaveNoError_And_BeSuccessTrue()
    {
        var result = Result.Success();

        result.IsSuccess.Should().BeTrue();
        result.IsFailure.Should().BeFalse();
        result.Error.Should().Be(Error.None);
    }

    [Fact]
    public void Failure_Should_CarryError_And_BeSuccessFalse()
    {
        var error = Error.Validation("Topup.InvalidAmount", "Amount must be positive.");

        var result = Result.Failure(error);

        result.IsSuccess.Should().BeFalse();
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(error);
    }

    [Fact]
    public void Success_With_Error_Should_Throw()
    {
        var act = () => new Result(true, Error.Validation("X", "Y"));
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Failure_With_None_Should_Throw()
    {
        var act = () => new Result(false, Error.None);
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Generic_Success_Should_Carry_Value()
    {
        var result = Result.Success(42);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(42);
        result.GetSuccessValue().Should().Be(42);
    }

    [Fact]
    public void Generic_GetSuccessValue_OnFailure_Should_Throw()
    {
        var result = Result.Failure<int>(Error.NotFound("X", "missing"));

        var act = () => result.GetSuccessValue();
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Generic_Or_Should_Return_Fallback_When_Failed()
    {
        var result = Result.Failure<int>(Error.Failure("X", "Y"));

        result.Or(99).Should().Be(99);
    }

    [Fact]
    public void Generic_Map_Should_Transform_Value_On_Success()
    {
        var result = Result.Success(5);

        var mapped = result.Map(x => x * 2);

        mapped.IsSuccess.Should().BeTrue();
        mapped.Value.Should().Be(10);
    }

    [Fact]
    public void Generic_Map_Should_Propagate_Error_On_Failure()
    {
        var error = Error.Failure("X", "Y");
        var result = Result.Failure<int>(error);

        var mapped = result.Map(x => x * 2);

        mapped.IsSuccess.Should().BeFalse();
        mapped.Error.Should().Be(error);
    }

    [Fact]
    public void Generic_Bind_Should_Chain_Successful_Results()
    {
        var result = Result.Success(5);

        var bound = result.Bind(x => Result.Success(x.ToString(System.Globalization.CultureInfo.InvariantCulture)));

        bound.IsSuccess.Should().BeTrue();
        bound.Value.Should().Be("5");
    }

    [Fact]
    public void Generic_Bind_Should_ShortCircuit_On_Failure()
    {
        var error = Error.Conflict("X", "Y");
        var result = Result.Failure<int>(error);

        var bound = result.Bind(x => Result.Success(x.ToString(System.Globalization.CultureInfo.InvariantCulture)));

        bound.IsSuccess.Should().BeFalse();
        bound.Error.Should().Be(error);
    }

    [Fact]
    public void Generic_Ensure_Should_Keep_Success_When_Predicate_True()
    {
        var result = Result.Success(5);

        var ensured = result.Ensure(x => x > 0, Error.Failure("X", "Y"));

        ensured.IsSuccess.Should().BeTrue();
        ensured.Value.Should().Be(5);
    }

    [Fact]
    public void Generic_Ensure_Should_Fail_When_Predicate_False()
    {
        var result = Result.Success(5);
        var error = Error.Failure("X", "Y");

        var ensured = result.Ensure(x => x > 100, error);

        ensured.IsSuccess.Should().BeFalse();
        ensured.Error.Should().Be(error);
    }

    [Fact]
    public void Generic_Implicit_Conversion_From_Value_Should_Succeed()
    {
        Result<int> result = 42;

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(42);
    }

    [Fact]
    public void Generic_Implicit_Conversion_From_Error_Should_Fail()
    {
        var error = Error.NotFound("X", "Y");
        Result<int> result = error;

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(error);
    }

    [Fact]
    public void Error_Factories_Should_Set_Correct_Type()
    {
        Error.Validation("c", "m").Type.Should().Be(ErrorType.Validation);
        Error.Conflict("c", "m").Type.Should().Be(ErrorType.Conflict);
        Error.NotFound("c", "m").Type.Should().Be(ErrorType.NotFound);
        Error.Unauthorized("c", "m").Type.Should().Be(ErrorType.Unauthorized);
        Error.Forbidden("c", "m").Type.Should().Be(ErrorType.Forbidden);
        Error.Unavailable("c", "m").Type.Should().Be(ErrorType.Unavailable);
        Error.Failure("c", "m").Type.Should().Be(ErrorType.Failure);
    }

    [Fact]
    public void Error_None_Should_Be_IsNone_True()
    {
        Error.None.IsNone.Should().BeTrue();
        Error.Failure("c", "m").IsNone.Should().BeFalse();
    }

    [Fact]
    public void Error_ToString_Should_Format_Correctly()
    {
        var error = Error.Validation("Topup.InvalidAmount", "Amount must be positive.");
        error.ToString().Should().Be("[Validation] Topup.InvalidAmount: Amount must be positive.");
    }

    [Fact]
    public void Ensure_On_NonGeneric_Should_Propagate_Failure()
    {
        var error = Error.Failure("X", "Y");
        var result = Result.Failure(error);

        var ensured = result.Ensure(() => true, Error.Failure("Z", "W"));

        ensured.IsFailure.Should().BeTrue();
        ensured.Error.Should().Be(error);
    }
}
