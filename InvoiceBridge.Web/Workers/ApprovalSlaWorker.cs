using InvoiceBridge.Application.Abstractions.Services;
using Microsoft.Extensions.Options;

namespace InvoiceBridge.Web.Workers;

public sealed class ApprovalSlaWorker(
    IServiceScopeFactory scopeFactory,
    IOptionsMonitor<WorkerOptions> optionsMonitor,
    ILogger<ApprovalSlaWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("ApprovalSlaWorker starting");

        while (!stoppingToken.IsCancellationRequested)
        {
            var options = optionsMonitor.CurrentValue.ApprovalSla;

            if (!options.Enabled)
            {
                await Task.Delay(TimeSpan.FromSeconds(options.PollIntervalSeconds), stoppingToken);
                continue;
            }

            try
            {
                using var scope = scopeFactory.CreateScope();
                var approvalService = scope.ServiceProvider.GetRequiredService<IApprovalService>();

                // ListPendingAsync evaluates SLA state and publishes breach/escalation
                // notifications as a side effect when thresholds are first crossed.
                // Passing an empty role filters across all pending approvals.
                var pending = await approvalService.ListPendingAsync(role: string.Empty, stoppingToken);

                if (pending.Count > 0)
                {
                    logger.LogDebug("ApprovalSlaWorker evaluated {Count} pending approval(s)", pending.Count);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "ApprovalSlaWorker iteration failed");
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

        logger.LogInformation("ApprovalSlaWorker stopping");
    }
}
