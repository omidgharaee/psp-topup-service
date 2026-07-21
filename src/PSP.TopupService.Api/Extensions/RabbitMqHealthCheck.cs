using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace PSP.TopupService.Api.Extensions;

/// <summary>
/// Lightweight RabbitMQ reachability check using a raw TCP connect. Avoids the
/// version churn of the HealthChecks.Rabbitmq connection-factory overloads
/// while still surfacing broker outages in the readiness probe.
/// </summary>
public sealed class RabbitMqHealthCheck : IHealthCheck
{
    private readonly IConfiguration _configuration;

    public RabbitMqHealthCheck(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var section = _configuration.GetSection("RabbitMq");
        var host = section["Host"] ?? "localhost";
        var port = int.Parse(section["Port"] ?? "5672", System.Globalization.CultureInfo.InvariantCulture);

        try
        {
            using var client = new System.Net.Sockets.TcpClient();
            await client.ConnectAsync(host, port);
            return HealthCheckResult.Healthy($"RabbitMQ {host}:{port} reachable");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Degraded($"RabbitMQ {host}:{port} unreachable", ex);
        }
    }
}
