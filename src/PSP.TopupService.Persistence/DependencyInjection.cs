using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PSP.TopupService.Application.Common.Abstractions;
using PSP.TopupService.Application.Common.Inbox;
using PSP.TopupService.Application.Common.Outbox;
using PSP.TopupService.Persistence.Context;
using PSP.TopupService.Persistence.Inbox;
using PSP.TopupService.Persistence.Interceptors;
using PSP.TopupService.Persistence.Outbox;
using PSP.TopupService.Persistence.Repositories;

namespace PSP.TopupService.Persistence;

/// <summary>
/// Persistence-layer DI registration. Host projects (Api, Worker) call
/// <see cref="AddPersistence"/> to register the DbContext, repositories, the
/// unit of work, the outbox writer and the audit interceptor.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddPersistence(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("TopupDatabase")
            ?? throw new InvalidOperationException("Connection string 'TopupDatabase' is not configured.");

        services.AddSingleton<AuditSaveChangesInterceptor>();
        services.AddSingleton<OutboxMessageSerializer>();

        services.AddDbContext<TopupDbContext>((sp, options) =>
        {
            options
                .UseNpgsql(connectionString, npg =>
                {
                    npg.MigrationsAssembly(typeof(TopupDbContext).Assembly.FullName);
                    npg.EnableRetryOnFailure(maxRetryCount: 5, maxRetryDelay: TimeSpan.FromSeconds(3), errorCodesToAdd: null);
                })
                .AddInterceptors(sp.GetRequiredService<AuditSaveChangesInterceptor>())
                .UseSnakeCaseNamingConvention();
        });

        services.AddScoped<ITopupRepository, TopupRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IOutboxWriter, EfOutboxWriter>();

        // Outbox publisher and inbox store use a short-lived DbContext (the
        // background worker resolves them per iteration).
        services.AddScoped<IOutboxPublisher, EfOutboxPublisher>();
        services.AddScoped<IInboxStore, EfInboxStore>();

        return services;
    }
}
