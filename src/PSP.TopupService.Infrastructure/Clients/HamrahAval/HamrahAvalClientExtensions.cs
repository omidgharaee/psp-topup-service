using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PSP.TopupService.Application.Topups.Abstractions;
using PSP.TopupService.Infrastructure.Clients.Bank;
using PSP.TopupService.Infrastructure.Clients.Configuration;
using PSP.TopupService.Infrastructure.Clients.HamrahAval;

namespace PSP.TopupService.Infrastructure.Clients;

/// <summary>
/// DI extensions for the external HTTP clients. Each client owns its own
/// <c>HttpClient</c> (via IHttpClientFactory) and its own typed options.
/// </summary>
public static class HamrahAvalClientExtensions
{
    /// <summary>
    /// Registers the Hamrah-e-Aval client and binds its options. The client
    /// builds its own Polly pipeline internally (timeout/retry/circuit
    /// breaker/fallback) so the policies stay cohesive with the call site.
    /// </summary>
    public static IServiceCollection AddHamrahAvalClient(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<HamrahAvalOptions>(configuration.GetSection(HamrahAvalOptions.SectionName));

        // Resolve options eagerly so the client can construct its pipeline.
        services.AddSingleton<HamrahAvalOptions>(sp =>
        {
            var opts = new HamrahAvalOptions();
            configuration.GetSection(HamrahAvalOptions.SectionName).Bind(opts);
            return opts;
        });

        services.AddHttpClient<IHamrahAvalClient, HamrahAvalClient>();
        return services;
    }

    /// <summary>Registers the Bank client with its own Polly pipeline.</summary>
    public static IServiceCollection AddBankClient(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<BankOptions>(configuration.GetSection(BankOptions.SectionName));
        services.AddSingleton<BankOptions>(sp =>
        {
            var opts = new BankOptions();
            configuration.GetSection(BankOptions.SectionName).Bind(opts);
            return opts;
        });

        services.AddHttpClient<IBankClient, BankClient>();
        return services;
    }
}
