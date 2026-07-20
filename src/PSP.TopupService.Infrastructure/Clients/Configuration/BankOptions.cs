namespace PSP.TopupService.Infrastructure.Clients.Configuration;

/// <summary>Configuration for the Bank gateway HTTP client.</summary>
public sealed class BankOptions
{
    public const string SectionName = "Bank";

    public string BaseUrl { get; set; } = "http://localhost:5060";

    public string ApiKey { get; set; } = "test-key";

    public TimeSpan RequestTimeout { get; set; } = TimeSpan.FromSeconds(5);

    public int RetryCount { get; set; } = 3;

    public TimeSpan RetryBaseDelay { get; set; } = TimeSpan.FromMilliseconds(500);
}
