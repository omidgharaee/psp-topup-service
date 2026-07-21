using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace PSP.TopupService.Api.Extensions;

/// <summary>
/// Health-check wiring. The endpoints follow Kubernetes / readiness-probe
/// conventions:
///   /health/live  - liveness (process is up; no dependencies)
///   /health/ready - readiness (all dependencies are reachable)
///   /health       - aggregate, tags broken down for the UI
/// </summary>
public static class HealthCheckExtensions
{
    private static readonly string[] ReadyTags = ["ready"];
    private static readonly string[] MessagingTags = ["messaging", "ready"];
    private static readonly string[] ExternalTags = ["external", "ready"];
    private static readonly string[] DbTags = ["db", "ready"];

    public static IServiceCollection AddTopupHealthChecks(this IServiceCollection services)
    {
        services.AddHealthChecks()
            .AddNpgSql(
                connectionStringFactory: sp => sp.GetRequiredService<IConfiguration>().GetConnectionString("TopupDatabase")
                    ?? throw new InvalidOperationException("Connection string 'TopupDatabase' is not configured."),
                healthQuery: "SELECT 1;",
                name: "postgresql",
                failureStatus: HealthStatus.Unhealthy,
                tags: DbTags)
            .AddCheck<RabbitMqHealthCheck>("rabbitmq", HealthStatus.Degraded, MessagingTags)
            .AddUrlGroup(
                uri: new Uri("http://bank-mock/health"),
                name: "bank-placeholder",
                failureStatus: HealthStatus.Degraded,
                tags: ExternalTags)
            .AddUrlGroup(
                uri: new Uri("http://hamrah-aval-mock/health"),
                name: "hamrah-aval-placeholder",
                failureStatus: HealthStatus.Degraded,
                tags: ExternalTags);

        return services;
    }

    /// <summary>Maps the standard health endpoints with appropriate tag filters.</summary>
    public static WebApplication MapTopupHealthEndpoints(this WebApplication app)
    {
        // Liveness: process up, no dependency checks.
        app.MapHealthChecks("/health/live", new HealthCheckOptions
        {
            Predicate = _ => false,
            ResponseWriter = SimpleResponseWriter,
        });

        // Readiness: every dependency tagged "ready" must be healthy.
        app.MapHealthChecks("/health/ready", new HealthCheckOptions
        {
            Predicate = check => check.Tags.Contains("ready"),
            ResponseWriter = DetailedResponseWriter,
        });

        // Aggregate: all checks.
        app.MapHealthChecks("/health", new HealthCheckOptions
        {
            Predicate = _ => true,
            ResponseWriter = DetailedResponseWriter,
        });

        return app;
    }

    private static Task SimpleResponseWriter(HttpContext context, HealthReport report)
    {
        context.Response.ContentType = "application/json";
        return context.Response.WriteAsJsonAsync(new { status = report.Status.ToString(), checks = Array.Empty<object>() });
    }

    private static Task DetailedResponseWriter(HttpContext context, HealthReport report)
    {
        context.Response.ContentType = "application/json";
        var body = new
        {
            status = report.Status.ToString(),
            duration = report.TotalDuration.TotalMilliseconds,
            entries = report.Entries.ToDictionary(
                e => e.Key,
                e => new { status = e.Value.Status.ToString(), duration = e.Value.Duration.TotalMilliseconds, error = e.Value.Exception?.Message }),
        };
        return context.Response.WriteAsJsonAsync(body);
    }
}
