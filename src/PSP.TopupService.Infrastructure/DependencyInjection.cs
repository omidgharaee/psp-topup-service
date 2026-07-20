using MassTransit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PSP.TopupService.Infrastructure.Messaging.Abstractions;
using PSP.TopupService.Infrastructure.Messaging.Configuration;
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

            // Consumers are registered by feature modules (payment-consumer, etc.).
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

                bus.ConfigureEndpoints(context);
            });
        });

        services.AddScoped<IMessagePublisher, MassTransitMessagePublisher>();
        services.AddSingleton<IIntegrationEventMapper, IntegrationEventMapper>();

        return services;
    }
}
