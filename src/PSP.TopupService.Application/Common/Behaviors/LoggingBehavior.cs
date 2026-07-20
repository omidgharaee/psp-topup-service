using System.Diagnostics;
using MediatR;
using Microsoft.Extensions.Logging;
using PSP.TopupService.Application.Common.Context;

namespace PSP.TopupService.Application.Common.Behaviors;

/// <summary>
/// Emits structured, Persian-language log entries around each request: an
/// opening "شروع پردازش درخواست" entry with correlation id, and a closing entry
/// noting elapsed milliseconds. Logs flow into Serilog and carry correlation,
/// trace and request ids for end-to-end tracing.
/// </summary>
public sealed class LoggingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly ILogger<LoggingBehavior<TRequest, TResponse>> _logger;
    private readonly ICorrelationContext _correlation;

    public LoggingBehavior(ILogger<LoggingBehavior<TRequest, TResponse>> logger, ICorrelationContext correlation)
    {
        _logger = logger;
        _correlation = correlation;
    }

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        var requestName = typeof(TRequest).Name;
        var stopwatch = Stopwatch.StartNew();

        using (_logger.BeginScope(new Dictionary<string, object?>
        {
            ["CorrelationId"] = _correlation.CorrelationId,
            ["RequestId"] = _correlation.RequestId,
            ["TraceId"] = _correlation.TraceId ?? string.Empty,
            ["Actor"] = _correlation.Actor ?? string.Empty,
            ["RemoteIp"] = _correlation.RemoteIp ?? string.Empty,
            ["RequestName"] = requestName,
        }))
        {
            _logger.LogInformation(
                "شروع پردازش درخواست {RequestName} با شناسه همبستگی {CorrelationId}",
                requestName,
                _correlation.CorrelationId);

            try
            {
                var response = await next();
                stopwatch.Stop();

                _logger.LogInformation(
                    "پایان پردازش درخواست {RequestName} در {ElapsedMilliseconds} میلی‌ثانیه",
                    requestName,
                    stopwatch.ElapsedMilliseconds);

                return response;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();

                _logger.LogError(ex, "خطای پردازش درخواست {RequestName} پس از {ElapsedMilliseconds} میلی‌ثانیه", requestName, stopwatch.ElapsedMilliseconds);
                throw;
            }
        }
    }
}
