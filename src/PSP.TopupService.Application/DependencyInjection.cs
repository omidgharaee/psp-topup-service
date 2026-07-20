using System.Reflection;
using FluentValidation;
using Mapster;
using MapsterMapper;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using PSP.TopupService.Application.Common.Context;

namespace PSP.TopupService.Application;

/// <summary>
/// Application-layer DI registration. Host projects (Api, Worker) call
/// <see cref="AddApplication"/> to wire up MediatR, FluentValidation, Mapster
/// and the correlation context. Persistence/Infrastructure registers the
/// concrete repository/outbox implementations separately.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        var assembly = Assembly.GetExecutingAssembly();

        // MediatR with pipeline behaviors, registered in execution order:
        // 1. UnhandledException (outermost — catches anything that escapes)
        // 2. Logging (scope + entry/exit)
        // 3. Validation (rejects bad input before it reaches the handler)
        // 4. Performance (warns on slow handlers)
        // 5. Transaction (innermost — commits after the handler returns)
        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(assembly);
            cfg.AddOpenBehavior(typeof(Common.Behaviors.UnhandledExceptionBehavior<,>));
            cfg.AddOpenBehavior(typeof(Common.Behaviors.LoggingBehavior<,>));
            cfg.AddOpenBehavior(typeof(Common.Behaviors.ValidationBehavior<,>));
            cfg.AddOpenBehavior(typeof(Common.Behaviors.PerformanceBehavior<,>));
            cfg.AddOpenBehavior(typeof(Common.Behaviors.TransactionBehavior<,>));
        });

        // FluentValidation: auto-register every AbstractValidator<T> in this assembly.
        services.AddValidatorsFromAssembly(assembly);

        // Mapster: scan IRegister implementations.
        var mapsterConfig = TypeAdapterConfig.GlobalSettings;
        mapsterConfig.Scan(assembly);
        services.AddSingleton(mapsterConfig);
        services.AddScoped<IMapper, ServiceMapper>();

        // Correlation context (per-request, async-local backed).
        services.AddSingleton<ICorrelationContext, CorrelationContext>();

        return services;
    }
}
