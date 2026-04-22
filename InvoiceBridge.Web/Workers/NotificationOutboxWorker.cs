using InvoiceBridge.Application.Abstractions.Services;
using Microsoft.Extensions.Options;

namespace InvoiceBridge.Web.Workers;

public sealed class NotificationOutboxWorker(
    IServiceScopeFactory scopeFactory,
    IOptionsMonitor<WorkerOptions> optionsMonitor,
    ILogger<NotificationOutboxWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("NotificationOutboxWorker starting");

        while (!stoppingToken.IsCancellationRequested)
        {
            var options = optionsMonitor.CurrentValue.NotificationOutbox;

            if (!options.Enabled)
            {
                await Task.Delay(TimeSpan.FromSeconds(options.PollIntervalSeconds), stoppingToken);
                continue;
            }

            try
            {
                using var scope = scopeFactory.CreateScope();
                var dispatcher = scope.ServiceProvider.GetRequiredService<INotificationOutboxDispatcher>();
                var dispatched = await dispatcher.DispatchPendingAsync(options.BatchSize, stoppingToken);

                if (dispatched > 0)
                {
                    logger.LogInformation("NotificationOutboxWorker dispatched {Count} message(s)", dispatched);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "NotificationOutboxWorker iteration failed");
            }

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(options.PollIntervalSeconds), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }

        logger.LogInformation("NotificationOutboxWorker stopping");
    }
}
