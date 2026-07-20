using FluentValidation;
using MediatR;
using PSP.TopupService.Application.Common.Behaviors;
using AppValidationException = PSP.TopupService.Application.Common.Exceptions.ValidationException;

namespace PSP.TopupService.UnitTests.Application.Topups.Commands;

/// <summary>
/// Verifies that <see cref="ValidationBehavior{TRequest,TResponse}"/> aggregates
/// failures and lets valid requests through untouched.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Layer", "Application")]
public class ValidationBehaviorTests
{
    private static RequestHandlerDelegate<SampleResponse> NextReturning(int value) =>
        _ => Task.FromResult(new SampleResponse { Value = value });

    [Fact]
    public async Task Should_Let_Valid_Request_Through()
    {
        var behavior = new ValidationBehavior<SampleRequest, SampleResponse>(new[] { new SampleValidator() });

        var response = await behavior.Handle(new SampleRequest { Value = 10 }, NextReturning(10), CancellationToken.None);

        response.Value.Should().Be(10);
    }

    [Fact]
    public async Task Should_Throw_AppValidationException_When_Invalid()
    {
        var behavior = new ValidationBehavior<SampleRequest, SampleResponse>(new[] { new SampleValidator() });

        var act = () => behavior.Handle(new SampleRequest { Value = -1 }, NextReturning(-1), CancellationToken.None);

        var ex = await act.Should().ThrowAsync<AppValidationException>();
        ex.Which.Errors.Should().ContainKey("Value");
    }

    [Fact]
    public async Task Should_Pass_Through_When_No_Validators_Registered()
    {
        var behavior = new ValidationBehavior<SampleRequest, SampleResponse>(Array.Empty<IValidator<SampleRequest>>());

        var response = await behavior.Handle(new SampleRequest { Value = -1 }, NextReturning(-1), CancellationToken.None);

        response.Value.Should().Be(-1);
    }

    public sealed record SampleRequest : IRequest<SampleResponse>
    {
        public int Value { get; init; }
    }

    public sealed record SampleResponse
    {
        public int Value { get; init; }
    }

    public sealed class SampleValidator : AbstractValidator<SampleRequest>
    {
        public SampleValidator()
        {
            RuleFor(x => x.Value).GreaterThanOrEqualTo(0);
        }
    }
}
