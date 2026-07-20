using MassTransit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PSP.TopupService.Infrastructure.Messaging.Abstractions;
using PSP.TopupService.Infrastructure.Messaging.Configuration;
using PSP.TopupService.Infrastructure.Messaging.Consumers;
using PSP.TopupService.Infrastructure.Messaging.Publishers;

namespace PSP.TopupService.Infrastructure;

/// <summary>
/// Infrastructure-layer DI registration. Host projects call
/// <see cref="AddInfrastructure"/> to wire up the message bus (MassTransit +
/// RabbitMQ), the message publisher and resilient HTTP clients. Specific
/// clients (Bank, Hamrah-e-Aval) register their own extensions.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<RabbitMqOptions>(configuration.GetSection(RabbitMqOptions.SectionName));

        services.AddMassTransit(cfg =>
        {
            cfg.SetKebabCaseEndpointNameFormatter();

            // Consumers are registered here. Each consumer type maps to its own
            // queue (kebab-case-named) so a failing consumer does not block others.
            cfg.AddConsumer<PaymentCompletedConsumer>();

            cfg.UsingRabbitMq((context, bus) =>
            {
                var options = configuration
                    .GetSection(RabbitMqOptions.SectionName)
                    .Get<RabbitMqOptions>() ?? new RabbitMqOptions();

                // Build the AMQP URI so host/port/virtual-host/credentials are all explicit.
                var vhost = string.IsNullOrEmpty(options.VirtualHost) || options.VirtualHost == "/"
                    ? string.Empty
                    : $"/{Uri.EscapeDataString(options.VirtualHost)}";
                var uri = new Uri($"amqp://{options.Host}:{options.Port}{vhost}");

                bus.Host(uri, h =>
                {
                    h.Username(options.Username);
                    h.Password(options.Password);
                });

                // MassTransit retry for transient broker issues. Business-level
                // idempotency (inbox) ensures retries never double-execute work.
                bus.UseMessageRetry(r => r.Intervals(100, 500, 1000));

                bus.ConfigureEndpoints(context);
            });
        });

        services.AddScoped<IMessagePublisher, MassTransitMessagePublisher>();
        services.AddSingleton<IIntegrationEventMapper, IntegrationEventMapper>();

        return services;
    }
}
