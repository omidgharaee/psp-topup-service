using System.Net;
using System.Text;
using PSP.TopupService.Application.Topups.Clients;
using PSP.TopupService.Domain.Topups.ValueObjects;
using PSP.TopupService.Infrastructure.Clients.Configuration;
using PSP.TopupService.Infrastructure.Clients.HamrahAval;

namespace PSP.TopupService.UnitTests.Infrastructure.Clients;

/// <summary>
/// Verifies the Hamrah-e-Aval client end-to-end against an in-memory HTTP
/// handler. Confirms the happy path and that Polly's pipeline converts
/// terminal failures into <see cref="HamrahAvalException"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Layer", "Infrastructure")]
public class HamrahAvalClientTests
{
    private static HamrahAvalOptions FastOptions() => new()
    {
        BaseUrl = "http://test",
        ApiKey = "k",
        RequestTimeout = TimeSpan.FromMilliseconds(500),
        RetryCount = 2,
        RetryBaseDelay = TimeSpan.FromMilliseconds(5),
        CircuitBreakerThreshold = 100,
        CircuitBreakerDuration = TimeSpan.FromSeconds(1),
    };

    [Fact]
    public async Task TopupAsync_Should_Return_Reference_On_Success()
    {
        var handler = new StubHandler(req => StubHandler.Json(new { succeeded = true, reference = "MCI-OK" }));
        var client = BuildClient(handler);

        var result = await client.TopupAsync(Guid.NewGuid(), MobileNumber.Create("09121234567"), Money.Create(50_000), Guid.NewGuid());

        result.ProviderReference.Value.Should().Be("MCI-OK");
        result.ProviderReference.Source.Should().Be("MCI");
    }

    [Fact]
    public async Task TopupAsync_Should_Retry_On_5xx_Then_Succeed()
    {
        var calls = 0;
        var handler = new StubHandler(req =>
        {
            calls++;
            return calls < 2
                ? new HttpResponseMessage(HttpStatusCode.InternalServerError)
                : StubHandler.Json(new { succeeded = true, reference = "MCI-OK" });
        });

        var client = BuildClient(handler);

        var result = await client.TopupAsync(Guid.NewGuid(), MobileNumber.Create("09121234567"), Money.Create(50_000), Guid.NewGuid());

        result.ProviderReference.Value.Should().Be("MCI-OK");
        calls.Should().BeGreaterThan(1);
    }

    [Fact]
    public async Task TopupAsync_Should_Throw_HamrahAvalException_When_All_Retries_Fail()
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError));
        var client = BuildClient(handler);

        var act = () => client.TopupAsync(Guid.NewGuid(), MobileNumber.Create("09121234567"), Money.Create(50_000), Guid.NewGuid());

        var ex = await act.Should().ThrowAsync<HamrahAvalException>();
        ex.Which.TopupId.Should().NotBeEmpty();
    }

    [Fact]
    public async Task TopupAsync_Should_Throw_HamrahAvalException_When_Provider_Returns_Failed_Body()
    {
        var handler = new StubHandler(_ => StubHandler.Json(new { succeeded = false, errorMessage = "Invalid number" }));
        var client = BuildClient(handler);

        var act = () => client.TopupAsync(Guid.NewGuid(), MobileNumber.Create("09121234567"), Money.Create(50_000), Guid.NewGuid());

        await act.Should().ThrowAsync<HamrahAvalException>();
    }

    private static HamrahAvalClient BuildClient(StubHandler handler)
    {
        var http = new HttpClient(handler) { BaseAddress = new Uri("http://test") };
        return new HamrahAvalClient(http, FastOptions(), Microsoft.Extensions.Logging.Abstractions.NullLogger<HamrahAvalClient>.Instance);
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _respond;

        public StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) => _respond = respond;

        public static HttpResponseMessage Json(object body) => new(HttpStatusCode.OK)
        {
            Content = new StringContent(System.Text.Json.JsonSerializer.Serialize(body), Encoding.UTF8, "application/json"),
        };

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(_respond(request));
    }
}
