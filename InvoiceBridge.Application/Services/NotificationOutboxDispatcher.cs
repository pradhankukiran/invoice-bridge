using System.Text.Json;
using InvoiceBridge.Application.Abstractions.Persistence;
using InvoiceBridge.Application.Abstractions.Services;
using InvoiceBridge.Application.DTOs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace InvoiceBridge.Application.Services;

internal sealed class NotificationOutboxDispatcher(
    IApplicationDbContext dbContext,
    INotificationDigestSender digestSender,
    ILogger<NotificationOutboxDispatcher> logger) : INotificationOutboxDispatcher
{
    private const int MaxAttempts = 8;

    public async Task<int> DispatchPendingAsync(int maxBatch, CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        var claim = Math.Clamp(maxBatch, 1, 100);

        // DateTimeOffset operations aren't server-translatable on SQLite (in-memory test path);
        // fetch candidates with SQL-safe filters, then apply retry-window + ordering client-side.
        var candidates = await dbContext.NotificationOutbox
            .Where(m => m.DispatchedAtUtc == null && m.AttemptCount < MaxAttempts)
            .OrderBy(m => m.Id)
            .Take(claim * 4)
            .ToListAsync(cancellationToken);

        var pending = candidates
            .Where(m => m.NextAttemptAtUtc == null || m.NextAttemptAtUtc <= now)
            .OrderBy(m => m.CreatedAtUtc)
            .Take(claim)
            .ToList();

        if (pending.Count == 0)
        {
            return 0;
        }

        var dispatched = 0;
        foreach (var message in pending)
        {
            try
            {
                var recipients = JsonSerializer.Deserialize<List<string>>(message.RecipientsJson) ?? [];

                await digestSender.SendAsync(new NotificationDigestMessage
                {
                    Recipients = recipients,
                    Subject = message.Subject,
                    Body = message.Body
                }, cancellationToken);

                message.DispatchedAtUtc = DateTimeOffset.UtcNow;
                message.NextAttemptAtUtc = null;
                message.LastError = null;
                message.AttemptCount += 1;
                dispatched++;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                message.AttemptCount += 1;
                message.LastError = ex.Message.Length > 1000 ? ex.Message[..1000] : ex.Message;
                message.NextAttemptAtUtc = ComputeBackoff(message.AttemptCount);

                logger.LogWarning(ex,
                    "Failed to dispatch notification outbox message {OutboxId} (attempt {Attempt}); next retry {Next:O}",
                    message.Id, message.AttemptCount, message.NextAttemptAtUtc);
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return dispatched;
    }

    private static DateTimeOffset ComputeBackoff(int attempt)
    {
        // Exponential: 30s, 60s, 2m, 4m, 8m, 16m, 30m cap
        var seconds = Math.Min(30 * Math.Pow(2, attempt - 1), 1800);
        return DateTimeOffset.UtcNow.AddSeconds(seconds);
    }
}
