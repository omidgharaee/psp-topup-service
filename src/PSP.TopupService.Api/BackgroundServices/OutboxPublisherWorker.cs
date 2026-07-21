using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PSP.TopupService.Api.Options;
using PSP.TopupService.Application.Common.Context;
using PSP.TopupService.Contracts;
using PSP.TopupService.Infrastructure.Messaging.Abstractions;
using PSP.TopupService.Persistence.Context;
using PSP.TopupService.Persistence.Outbox;

namespace PSP.TopupService.Api.BackgroundServices;

/// <summary>
/// Background service that drains the transactional outbox. On every cycle it
/// reclaims expired leases, leases a batch of pending rows, publishes each one
/// to RabbitMQ via MassTransit and marks the outcome. Designed for safe
/// horizontal scaling — concurrent workers never lease the same row (atomic
/// SQL lease, see <see cref="IOutboxPublisher"/>).
/// </summary>
public sealed class OutboxPublisherWorker : BackgroundService
{
    private readonly IServiceProvider _services;
    private readonly IOptions<OutboxPublisherOptions> _options;
    private readonly ILogger<OutboxPublisherWorker> _logger;

    public OutboxPublisherWorker(
        IServiceProvider services,
        IOptions<OutboxPublisherOptions> options,
        ILogger<OutboxPublisherWorker> logger)
    {
        _services = services;
        _options = options;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("سرویس انتشار Outbox راه‌اندازی شد");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunCycleAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // Graceful shutdown in progress.
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطای غیرمنتظره در چرخه انتشار Outbox");
            }

            try
            {
                await Task.Delay(_options.Value.PollingInterval, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }

        _logger.LogInformation("سرویس انتشار Outbox متوقف شد");
    }

    private async Task RunCycleAsync(CancellationToken stoppingToken)
    {
        await using var scope = _services.CreateAsyncScope();
        var publisher = scope.ServiceProvider.GetRequiredService<IOutboxPublisher>();
        var mapper = scope.ServiceProvider.GetRequiredService<IIntegrationEventMapper>();
        var messagePublisher = scope.ServiceProvider.GetRequiredService<IMessagePublisher>();
        var correlation = scope.ServiceProvider.GetRequiredService<ICorrelationContext>();

        if (_options.Value.ReclaimExpiredLeases)
        {
            var reclaimed = await publisher.ReclaimExpiredLeasesAsync(stoppingToken);
            if (reclaimed > 0)
            {
                _logger.LogWarning("بازیابی {Count} پیام Outbox با lease منقضی‌شده", reclaimed);
            }
        }

        var batch = await publisher.LeasePendingAsync(_options.Value.BatchSize, _options.Value.LeaseDuration, stoppingToken);
        if (batch.Count == 0)
        {
            return;
        }

        _logger.LogInformation("اجاره {Count} پیام Outbox برای انتشار", batch.Count);

        foreach (var message in batch)
        {
            await PublishOneAsync(publisher, mapper, messagePublisher, correlation, message, stoppingToken);
        }
    }

    private async Task PublishOneAsync(
        IOutboxPublisher publisher,
        IIntegrationEventMapper mapper,
        IMessagePublisher messagePublisher,
        ICorrelationContext correlation,
        Persistence.Outbox.OutboxMessage message,
        CancellationToken cancellationToken)
    {
        // Populate the correlation context so MassTransit headers and Serilog
        // scopes carry the right ids for this message.
        correlation.Set(message.CorrelationId, message.Id, null, null, null);

        try
        {
            var integrationEvent = mapper.Map(message.Type, message.Payload);
            if (integrationEvent is null)
            {
                await publisher.MarkFailedAsync(message.Id, $"Unknown event type '{message.Type}'.", cancellationToken);
                _logger.LogError("نوع رویداد ناشناخته: {EventType} (پیام {MessageId})", message.Type, message.Id);
                return;
            }

            await messagePublisher.PublishAsync(integrationEvent, message.Id, message.CorrelationId, cancellationToken);
            await publisher.MarkPublishedAsync(message.Id, cancellationToken);

            _logger.LogInformation(
                "انتشار موفق {EventType} (پیام {MessageId})",
                integrationEvent.EventType,
                message.Id);
        }
        catch (Exception ex)
        {
            await publisher.MarkFailedAsync(message.Id, ex.Message, cancellationToken);
            _logger.LogError(ex, "خطا در انتشار پیام {MessageId} از نوع {EventType} - تلاش شماره {Attempt}", message.Id, message.Type, message.AttemptCount);
        }
    }
}
