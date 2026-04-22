using InvoiceBridge.Application.Abstractions.Services;
using InvoiceBridge.Application.DTOs;
using Microsoft.Extensions.Options;

namespace InvoiceBridge.Web.Workers;

public sealed class ImportQueueWorker(
    IServiceScopeFactory scopeFactory,
    IOptionsMonitor<WorkerOptions> optionsMonitor,
    ILogger<ImportQueueWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("ImportQueueWorker starting");

        while (!stoppingToken.IsCancellationRequested)
        {
            var options = optionsMonitor.CurrentValue.ImportQueue;

            if (!options.Enabled)
            {
                await Task.Delay(TimeSpan.FromSeconds(options.PollIntervalSeconds), stoppingToken);
                continue;
            }

            try
            {
                using var scope = scopeFactory.CreateScope();
                var invoiceService = scope.ServiceProvider.GetRequiredService<IInvoiceService>();

                var result = await invoiceService.ProcessImportQueueAsync(new ProcessImportQueueRequest
                {
                    BatchSize = options.BatchSize,
                    ProcessedBy = options.ProcessedBy
                }, stoppingToken);

                if (result.ProcessedCount > 0)
                {
                    logger.LogInformation(
                        "ImportQueueWorker batch processed {ProcessedCount} imports ({Succeeded} succeeded, {Failed} failed)",
                        result.ProcessedCount, result.SucceededCount, result.FailedCount);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "ImportQueueWorker iteration failed");
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

        logger.LogInformation("ImportQueueWorker stopping");
    }
}
