using System.Diagnostics;
using MediatR;
using Microsoft.Extensions.Logging;
using PSP.TopupService.Application.Common.Context;

namespace PSP.TopupService.Application.Common.Behaviors;

/// <summary>
/// Emits a warning when a request exceeds the configured SLA threshold, so
/// slow handlers can be identified in production without bespoke timing logs.
/// </summary>
public sealed class PerformanceBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private const long SlowRequestThresholdMs = 500;

    private readonly ILogger<PerformanceBehavior<TRequest, TResponse>> _logger;
    private readonly ICorrelationContext _correlation;

    public PerformanceBehavior(ILogger<PerformanceBehavior<TRequest, TResponse>> logger, ICorrelationContext correlation)
    {
        _logger = logger;
        _correlation = correlation;
    }

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        var response = await next();
        stopwatch.Stop();

        if (stopwatch.ElapsedMilliseconds > SlowRequestThresholdMs)
        {
            _logger.LogWarning(
                "هشدار کارایی: درخواست {RequestName} بیش از حد طول کشید ({ElapsedMilliseconds} میلی‌ثانیه) - شناسه همبستگی {CorrelationId}",
                typeof(TRequest).Name,
                stopwatch.ElapsedMilliseconds,
                _correlation.CorrelationId);
        }

        return response;
    }
}
