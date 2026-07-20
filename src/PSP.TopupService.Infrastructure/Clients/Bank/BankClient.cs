using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Timeout;
using PSP.TopupService.Application.Topups.Abstractions;
using PSP.TopupService.Domain.Topups.ValueObjects;
using PSP.TopupService.Infrastructure.Clients.Configuration;

namespace PSP.TopupService.Infrastructure.Clients.Bank;

/// <summary>
/// HTTP client for the Bank gateway's reversal endpoint. Like the MCI client,
/// it owns a Polly pipeline (timeout + retry on transient errors) so the
/// reverse flow only sees a definitive outcome. There is no circuit breaker
/// here: reversals are infrequent and must always be attempted.
/// </summary>
public sealed class BankClient : IBankClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly HttpClient _httpClient;
    private readonly BankOptions _options;
    private readonly ILogger<BankClient> _logger;
    private readonly AsyncPolicy<BankReversalResponse> _pipeline;

    public BankClient(HttpClient httpClient, BankOptions options, ILogger<BankClient> logger)
    {
        _httpClient = httpClient;
        _options = options;
        _logger = logger;
        _httpClient.BaseAddress = new Uri(options.BaseUrl);
        _httpClient.Timeout = options.RequestTimeout.Add(options.RequestTimeout);

        var timeout = Policy.TimeoutAsync<BankReversalResponse>(options.RequestTimeout);
        var retry = Policy<BankReversalResponse>
            .Handle<HttpRequestException>()
            .Or<TimeoutRejectedException>()
            .WaitAndRetryAsync(
                options.RetryCount,
                attempt => options.RetryBaseDelay * Math.Pow(2, attempt - 1),
                (exception, delay, attempt, ctx) =>
                {
                    _logger.LogWarning(exception.Exception, "تلاش مجدد برگشت وجه (شماره {Attempt})", attempt);
                });

        _pipeline = Policy.WrapAsync<BankReversalResponse>(retry, timeout);
    }

    public async Task<BankReversalResult> ReverseAsync(
        Guid topupId,
        TransactionReference originalPaymentReference,
        Money amount,
        Guid correlationId,
        CancellationToken cancellationToken = default)
    {
        var request = new
        {
            topupId,
            originalReference = originalPaymentReference.Value,
            amount = amount.Value,
            currency = amount.Currency,
            correlationId,
        };

        var body = await _pipeline.ExecuteAsync(async ct => await CallReverseAsync(request, ct), cancellationToken);

        return new BankReversalResult
        {
            ReversalReference = TransactionReference.Create(body.ReversalReference ?? Guid.NewGuid().ToString("N"), "REVERSAL"),
            ReversedAtUtc = DateTime.UtcNow,
        };
    }

    private async Task<BankReversalResponse> CallReverseAsync(object request, CancellationToken ct)
    {
        using var response = await _httpClient.PostAsJsonAsync("/payments/reverse", request, JsonOptions, ct);

        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"Bank reverse failed with HTTP {(int)response.StatusCode}");
        }

        return await response.Content.ReadFromJsonAsync<BankReversalResponse>(JsonOptions, ct)
            ?? throw new HttpRequestException("Empty Bank reverse response");
    }
}

internal sealed class BankReversalResponse
{
    public bool Succeeded { get; init; }
    public string? ReversalReference { get; init; }
    public string? ErrorMessage { get; init; }
}
