using MediatR;
using Microsoft.Extensions.Logging;
using PSP.TopupService.Application.Common.Abstractions;
using PSP.TopupService.Application.Common.Context;

namespace PSP.TopupService.Application.Common.Behaviors;

/// <summary>
/// Wraps commands that require atomicity in a single unit-of-work commit. The
/// behavior commits only if the handler returns successfully; exceptions
/// propagate and the underlying DbContext rolls back. Command handlers
/// therefore do not call SaveChanges themselves — the behavior centralises the
/// commit so transaction policy is consistent.
/// </summary>
public sealed class TransactionBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<TransactionBehavior<TRequest, TResponse>> _logger;
    private readonly ICorrelationContext _correlation;

    public TransactionBehavior(
        IUnitOfWork unitOfWork,
        ILogger<TransactionBehavior<TRequest, TResponse>> logger,
        ICorrelationContext correlation)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
        _correlation = correlation;
    }

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        var isCommand = typeof(TRequest).Name.EndsWith("Command", StringComparison.Ordinal);
        if (!isCommand)
        {
            // Queries do not mutate state; skip the commit cost.
            return await next();
        }

        var response = await next();

        try
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogDebug(
                "ذخیره‌سازی تراکنش موفق بود - شناسه همبستگی {CorrelationId}",
                _correlation.CorrelationId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "خطا در ذخیره‌سازی تراکنش - شناسه همبستگی {CorrelationId}", _correlation.CorrelationId);
            throw;
        }

        return response;
    }
}
