using MediatR;
using Microsoft.Extensions.Logging;
using PSP.TopupService.Application.Common.Context;
using PSP.TopupService.Application.Common.Exceptions;

namespace PSP.TopupService.Application.Common.Behaviors;

/// <summary>
/// Last line of defence: catches any exception that escaped the handler and
/// logs it with full context before re-throwing. Application-layer exceptions
/// (Validation, Business) are allowed to propagate untouched so the global
/// middleware can map them; only truly unexpected exceptions are logged here
/// at <c>Error</c> level.
/// </summary>
public sealed class UnhandledExceptionBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly ILogger<UnhandledExceptionBehavior<TRequest, TResponse>> _logger;
    private readonly ICorrelationContext _correlation;

    public UnhandledExceptionBehavior(ILogger<UnhandledExceptionBehavior<TRequest, TResponse>> logger, ICorrelationContext correlation)
    {
        _logger = logger;
        _correlation = correlation;
    }

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        try
        {
            return await next();
        }
        catch (ValidationException)
        {
            // Already a validation failure — let the API middleware handle the response shape.
            throw;
        }
        catch (SharedKernel.Exceptions.DomainException)
        {
            // Business-rule exception; mapped to the appropriate HTTP status by middleware.
            throw;
        }
        catch (OperationCanceledException)
        {
            // Graceful cancellation; not an error.
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "خطای غیرمنتظره در پردازش درخواست {RequestName} - شناسه همبستگی {CorrelationId}", typeof(TRequest).Name, _correlation.CorrelationId);
            throw;
        }
    }
}
