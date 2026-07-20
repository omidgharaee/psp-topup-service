using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Timeout;
using Polly.Wrap;
using PSP.TopupService.Application.Topups.Abstractions;
using PSP.TopupService.Application.Topups.Clients;
using PSP.TopupService.Domain.Topups.ValueObjects;
using PSP.TopupService.Infrastructure.Clients.Configuration;

namespace PSP.TopupService.Infrastructure.Clients.HamrahAval;

/// <summary>
/// HTTP client for the Hamrah-e-Aval (MCI) topup provider. The call is wrapped
/// in a Polly resilience pipeline (timeout -> retry with exponential backoff ->
/// circuit breaker -> fallback) so transient failures are absorbed and terminal
/// failures raise a typed <see cref="HamrahAvalException"/> for the reverse
/// flow to handle.
/// </summary>
public sealed class HamrahAvalClient : IHamrahAvalClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly HttpClient _httpClient;
    private readonly HamrahAvalOptions _options;
    private readonly ILogger<HamrahAvalClient> _logger;
    private readonly AsyncPolicyWrap<HamrahAvalTopupResult> _pipeline;

    public HamrahAvalClient(HttpClient httpClient, HamrahAvalOptions options, ILogger<HamrahAvalClient> logger)
    {
        _httpClient = httpClient;
        _options = options;
        _logger = logger;
        _httpClient.BaseAddress = new Uri(options.BaseUrl);
        _httpClient.Timeout = options.RequestTimeout.Add(options.RequestTimeout);
        _pipeline = BuildResiliencePipeline();
    }

    public async Task<HamrahAvalTopupResult> TopupAsync(
        Guid topupId,
        MobileNumber mobileNumber,
        Money amount,
        Guid correlationId,
        CancellationToken cancellationToken = default)
    {
        var context = new Context
        {
            ["TopupId"] = topupId.ToString(),
            ["CorrelationId"] = correlationId.ToString(),
        };

        return await _pipeline.ExecuteAsync(
            (ctx, ct) => CallProviderAsync(topupId, mobileNumber, amount, correlationId, ct),
            context,
            cancellationToken);
    }

    private async Task<HamrahAvalTopupResult> CallProviderAsync(
        Guid topupId, MobileNumber mobile, Money amount, Guid correlationId, CancellationToken ct)
    {
        var request = new
        {
            topupId,
            mobileNumber = mobile.Value,
            amount = amount.Value,
            currency = amount.Currency,
            correlationId,
        };

        using var response = await _httpClient.PostAsJsonAsync("/topup", request, JsonOptions, ct);

        if (response.StatusCode == HttpStatusCode.ServiceUnavailable || response.StatusCode == HttpStatusCode.InternalServerError)
        {
            _logger.LogWarning("خطای موقت از همراه اول (HTTP {Status}) - تلاش مجدد", response.StatusCode);
            throw new HamrahAvalTransientException($"HTTP {(int)response.StatusCode}");
        }

        if (!response.IsSuccessStatusCode)
        {
            throw new HamrahAvalException(topupId, $"HTTP {(int)response.StatusCode} از همراه اول");
        }

        var body = await response.Content.ReadFromJsonAsync<HamrahAvalResponse>(JsonOptions, ct);
        if (body is null || !body.Succeeded)
        {
            throw new HamrahAvalException(topupId, body?.ErrorMessage ?? "پاسخ نامعتبر از همراه اول");
        }

        return new HamrahAvalTopupResult
        {
            ProviderReference = TransactionReference.Create(body.Reference ?? Guid.NewGuid().ToString("N"), "MCI"),
            CompletedAtUtc = DateTime.UtcNow,
        };
    }

    /// <summary>
    /// Builds the resilience pipeline in the order: timeout (outer) wraps
    /// retry (with exponential backoff) wraps circuit breaker wraps the
    /// fallback (innermost, converts the final exception to HamrahAvalException).
    /// </summary>
    private AsyncPolicyWrap<HamrahAvalTopupResult> BuildResiliencePipeline()
    {
        // Timeout: bound each individual attempt.
        var timeout = Policy.TimeoutAsync<HamrahAvalTopupResult>(_options.RequestTimeout);

        // Retry: only on transient errors (timeouts / 5xx / transient exception).
        var retry = Policy<HamrahAvalTopupResult>
            .Handle<HamrahAvalTransientException>()
            .Or<TimeoutRejectedException>()
            .Or<HttpRequestException>()
            .WaitAndRetryAsync(
                retryCount: _options.RetryCount,
                sleepDurationProvider: attempt => _options.RetryBaseDelay * Math.Pow(2, attempt - 1),
                onRetry: (outcome, delay, attempt, ctx) =>
                {
                    var topupId = ctx.TryGetValue("TopupId", out var t) ? t : "?";
                    _logger.LogWarning(
                        "تلاش شماره {Attempt} برای همراه اول پس از تأخیر {DelayMs}ms - تراکنش {TopupId}",
                        attempt,
                        delay.TotalMilliseconds,
                        topupId);
                });

        // Circuit breaker: open after N consecutive transient failures.
        var circuitBreaker = Policy<HamrahAvalTopupResult>
            .Handle<HamrahAvalTransientException>()
            .Or<TimeoutRejectedException>()
            .Or<HttpRequestException>()
            .CircuitBreakerAsync(
                handledEventsAllowedBeforeBreaking: _options.CircuitBreakerThreshold,
                durationOfBreak: _options.CircuitBreakerDuration,
                onBreak: (outcome, state, ts, ctx) =>
                {
                    _logger.LogError(
                        "Circuit breaker باز شد به مدت {Duration}s - تراکنش {TopupId}",
                        _options.CircuitBreakerDuration.TotalSeconds,
                        ctx.TryGetValue("TopupId", out var t) ? t : "?");
                },
                onReset: ctx => _logger.LogInformation("Circuit breaker بسته شد (بازیابی شد)"),
                onHalfOpen: () => _logger.LogInformation("Circuit breaker در حالت نیمه‌باز - در حال آزمایش"));

        // Fallback: convert any final failure into a typed HamrahAvalException.
        // The onFallbackAsync delegate carries the context, so we read TopupId
        // from it before throwing inside the fallback action.
        var fallback = Policy<HamrahAvalTopupResult>
            .Handle<Exception>()
            .FallbackAsync(
                fallbackAction: (outcome, ctx, ct) =>
                {
                    var topupId = ctx.TryGetValue("TopupId", out var t) && Guid.TryParse(t as string, out var g)
                        ? g
                        : Guid.Empty;
                    throw new HamrahAvalException(topupId, "تمام تلاش‌های همراه اول شکست خورد", outcome.Exception);
                },
                onFallbackAsync: (outcome, ctx) =>
                {
                    _logger.LogError(
                        "خطای نهایی پس از تمام تلاش‌ها - تراکنش {TopupId}: {Error}",
                        ctx.TryGetValue("TopupId", out var t) ? t : "?",
                        outcome.Exception?.Message ?? "unknown");
                    return Task.CompletedTask;
                });

        // Wrap order: fallback(circuitBreaker(retry(timeout))) — outermost first.
        return fallback.WrapAsync(circuitBreaker.WrapAsync(retry.WrapAsync(timeout)));
    }
}

/// <summary>
/// Internal transient exception used to differentiate retryable failures
/// (timeout / 5xx) from terminal ones (4xx business rejection). Only retried
/// by Polly, never leaked to the caller.
/// </summary>
internal sealed class HamrahAvalTransientException : Exception
{
    public HamrahAvalTransientException(string message)
        : base(message)
    {
    }
}

/// <summary>Response body from the operator's /topup endpoint.</summary>
internal sealed class HamrahAvalResponse
{
    public bool Succeeded { get; init; }
    public string? Reference { get; init; }
    public string? ErrorMessage { get; init; }
}
