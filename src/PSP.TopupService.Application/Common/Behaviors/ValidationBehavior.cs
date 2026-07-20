using FluentValidation;
using MediatR;
using AppValidationException = PSP.TopupService.Application.Common.Exceptions.ValidationException;

namespace PSP.TopupService.Application.Common.Behaviors;

/// <summary>
/// Runs all FluentValidation validators registered for the incoming request
/// before the handler executes. Aggregated failures are surfaced as a
/// <see cref="ValidationException"/> which the API middleware maps to RFC7807
/// with a 400 status code and per-field errors.
/// </summary>
/// <typeparam name="TRequest">The request type.</typeparam>
/// <typeparam name="TResponse">The response type, which must be a Result or Result-like.</typeparam>
public sealed class ValidationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly IEnumerable<IValidator<TRequest>> _validators;

    public ValidationBehavior(IEnumerable<IValidator<TRequest>> validators)
    {
        _validators = validators;
    }

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        if (!_validators.Any())
        {
            return await next();
        }

        var context = new ValidationContext<TRequest>(request);
        var validationResults = await Task.WhenAll(
            _validators.Select(v => v.ValidateAsync(context, cancellationToken)));
        var failures = validationResults
            .SelectMany(r => r.Errors)
            .Where(f => f is not null)
            .ToList();

        if (failures.Count != 0)
        {
            throw new AppValidationException(failures);
        }

        return await next();
    }
}
