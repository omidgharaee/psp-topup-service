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
/// HTTP client for the Bank's Advice (finalization) endpoint. Owns a Polly
/// pipeline (timeout + retry). Unlike the reversal client, advice uses a
/// circuit breaker too: if the Bank is saturated we want the advice worker to
/// back off rather than pile up requests.
/// </summary>
public sealed class BankAdviceClient : IBankAdviceClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly HttpClient _httpClient;
    private readonly BankOptions _options;
    private readonly ILogger<BankAdviceClient> _logger;
    private readonly AsyncPolicy<BankAdviceResponse> _pipeline;

    public BankAdviceClient(HttpClient httpClient, BankOptions options, ILogger<BankAdviceClient> logger)
    {
        _httpClient = httpClient;
        _options = options;
        _logger = logger;
        _httpClient.BaseAddress = new Uri(options.BaseUrl);
        _httpClient.Timeout = options.RequestTimeout.Add(options.RequestTimeout);

        var timeout = Policy.TimeoutAsync<BankAdviceResponse>(options.RequestTimeout);
        var retry = Policy<BankAdviceResponse>
            .Handle<HttpRequestException>()
            .Or<TimeoutRejectedException>()
            .WaitAndRetryAsync(
                options.RetryCount,
                attempt => options.RetryBaseDelay * Math.Pow(2, attempt - 1),
                (outcome, delay, attempt, ctx) =>
                {
                    _logger.LogWarning(outcome.Exception, "تلاش مجدد Advice بانک (شماره {Attempt})", attempt);
                });

        _pipeline = Policy.WrapAsync<BankAdviceResponse>(retry, timeout);
    }

    public async Task<BankAdviceResult> AdviceAsync(
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

        var body = await _pipeline.ExecuteAsync(async ct => await CallAdviceAsync(request, ct), cancellationToken);

        return new BankAdviceResult
        {
            AdviceReference = TransactionReference.Create(body.AdviceReference ?? Guid.NewGuid().ToString("N"), "ADVICE"),
        };
    }

    private async Task<BankAdviceResponse> CallAdviceAsync(object request, CancellationToken ct)
    {
        using var response = await _httpClient.PostAsJsonAsync("/payments/advice", request, JsonOptions, ct);

        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"Bank advice failed with HTTP {(int)response.StatusCode}");
        }

        return await response.Content.ReadFromJsonAsync<BankAdviceResponse>(JsonOptions, ct)
            ?? throw new HttpRequestException("Empty Bank advice response");
    }
}

internal sealed class BankAdviceResponse
{
    public bool Succeeded { get; init; }
    public string? AdviceReference { get; init; }
    public string? ErrorMessage { get; init; }
}
