using System.Text.Json;
using InvoiceBridge.Application.Abstractions.Persistence;
using InvoiceBridge.Application.Abstractions.Services;
using InvoiceBridge.Application.Common;
using InvoiceBridge.Application.DTOs;
using InvoiceBridge.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace InvoiceBridge.Application.Services;

internal sealed class NotificationService(
    IApplicationDbContext dbContext,
    IRoleRecipientResolver roleRecipientResolver,
    INotificationOutboxDispatcher outboxDispatcher,
    ILogger<NotificationService> logger) : INotificationService, INotificationPublisher
{
    private const int DefaultMaxRows = 100;

    public async Task<int> GetUnreadCountAsync(string recipientUsername, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(recipientUsername))
        {
            return 0;
        }

        var normalizedRecipient = recipientUsername.Trim();

        return await dbContext.UserNotifications
            .CountAsync(
                item => item.RecipientUsername == normalizedRecipient && !item.IsRead,
                cancellationToken);
    }

    public async Task<IReadOnlyList<UserNotificationDto>> ListForUserAsync(NotificationListRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.RecipientUsername))
        {
            return [];
        }

        var normalizedRecipient = request.RecipientUsername.Trim();
        var maxRows = Math.Clamp(request.MaxRows <= 0 ? DefaultMaxRows : request.MaxRows, 1, 500);

        var query = dbContext.UserNotifications
            .AsNoTracking()
            .Where(item => item.RecipientUsername == normalizedRecipient);

        if (!request.IncludeRead)
        {
            query = query.Where(item => !item.IsRead);
        }

        var notifications = await query
            .Select(item => new UserNotificationDto(
                item.Id,
                item.Category,
                item.Severity,
                item.Title,
                item.Message,
                item.LinkUrl,
                item.IsRead,
                item.CreatedAtUtc,
                item.ReadAtUtc))
            .ToListAsync(cancellationToken);

        return notifications
            .OrderByDescending(item => item.CreatedAtUtc)
            .Take(maxRows)
            .ToList();
    }

    public async Task MarkAsReadAsync(int notificationId, string recipientUsername, CancellationToken cancellationToken = default)
    {
        if (notificationId <= 0 || string.IsNullOrWhiteSpace(recipientUsername))
        {
            return;
        }

        var normalizedRecipient = recipientUsername.Trim();

        var notification = await dbContext.UserNotifications
            .SingleOrDefaultAsync(item => item.Id == notificationId && item.RecipientUsername == normalizedRecipient, cancellationToken);

        if (notification is null || notification.IsRead)
        {
            return;
        }

        notification.IsRead = true;
        notification.ReadAtUtc = DateTimeOffset.UtcNow;

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task MarkAllAsReadAsync(string recipientUsername, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(recipientUsername))
        {
            return;
        }

        var normalizedRecipient = recipientUsername.Trim();

        var unreadNotifications = await dbContext.UserNotifications
            .Where(item => item.RecipientUsername == normalizedRecipient && !item.IsRead)
            .ToListAsync(cancellationToken);

        if (unreadNotifications.Count == 0)
        {
            return;
        }

        var now = DateTimeOffset.UtcNow;
        foreach (var notification in unreadNotifications)
        {
            notification.IsRead = true;
            notification.ReadAtUtc = now;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<int> PublishAsync(NotificationPublishRequest request, CancellationToken cancellationToken = default)
    {
        var recipients = request.RecipientUsernames
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Select(item => item.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (recipients.Count == 0)
        {
            return 0;
        }

        if (string.IsNullOrWhiteSpace(request.Title))
        {
            throw new ArgumentException("Notification title is required.", nameof(request.Title));
        }

        if (string.IsNullOrWhiteSpace(request.Message))
        {
            throw new ArgumentException("Notification message is required.", nameof(request.Message));
        }

        var notifications = recipients.Select(recipient => new UserNotification
        {
            RecipientUsername = recipient,
            Category = Truncate(string.IsNullOrWhiteSpace(request.Category) ? "General" : request.Category.Trim(), 64),
            Severity = Truncate(string.IsNullOrWhiteSpace(request.Severity) ? "Info" : request.Severity.Trim(), 16),
            Title = Truncate(request.Title.Trim(), 200),
            Message = Truncate(request.Message.Trim(), 1000),
            LinkUrl = string.IsNullOrWhiteSpace(request.LinkUrl) ? null : Truncate(request.LinkUrl.Trim(), 260),
            SourceEntityName = string.IsNullOrWhiteSpace(request.SourceEntityName) ? null : Truncate(request.SourceEntityName.Trim(), 80),
            SourceEntityId = string.IsNullOrWhiteSpace(request.SourceEntityId) ? null : Truncate(request.SourceEntityId.Trim(), 80),
            IsRead = false
        }).ToList();

        dbContext.UserNotifications.AddRange(notifications);

        if (request.SendDigest)
        {
            dbContext.NotificationOutbox.Add(new NotificationOutboxMessage
            {
                RecipientsJson = JsonSerializer.Serialize(recipients),
                Subject = Truncate(request.Title.Trim(), 200),
                Body = Truncate(request.Message.Trim(), 4000),
                CreatedAtUtc = DateTimeOffset.UtcNow,
                AttemptCount = 0
            });
        }

        AuditTrailWriter.Add(
            dbContext,
            entityName: "Notification",
            entityId: request.SourceEntityId ?? "bulk",
            action: "NotificationPublished",
            actor: string.IsNullOrWhiteSpace(request.Actor) ? "system" : request.Actor.Trim(),
            details: $"RecipientCount={recipients.Count}; Category={request.Category}; Severity={request.Severity}.");

        await dbContext.SaveChangesAsync(cancellationToken);

        if (request.SendDigest)
        {
            // Best-effort inline dispatch so recipients are notified immediately when
            // infrastructure is healthy. Persistence of the outbox row has already
            // committed above; the background OutboxDispatcher worker will retry
            // anything we don't dispatch synchronously, so swallow failures here.
            try
            {
                await outboxDispatcher.DispatchPendingAsync(maxBatch: 16, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex,
                    "Inline outbox dispatch failed after publishing notification; row remains for worker retry.");
            }
        }

        return notifications.Count;
    }

    public async Task<int> PublishToRoleAsync(string role, NotificationPublishRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(role))
        {
            return 0;
        }

        var recipients = await roleRecipientResolver.ResolveUsersByRoleAsync(role.Trim(), cancellationToken);
        var resolvedRequest = new NotificationPublishRequest
        {
            RecipientUsernames = recipients,
            Category = request.Category,
            Severity = request.Severity,
            Title = request.Title,
            Message = request.Message,
            LinkUrl = request.LinkUrl,
            SourceEntityName = request.SourceEntityName,
            SourceEntityId = request.SourceEntityId,
            Actor = request.Actor,
            SendDigest = request.SendDigest
        };

        return await PublishAsync(resolvedRequest, cancellationToken);
    }

    private static string Truncate(string value, int maxLength)
    {
        if (value.Length <= maxLength)
        {
            return value;
        }

        return value[..maxLength];
    }
}
