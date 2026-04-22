using InvoiceBridge.Application.Abstractions.Services;
using InvoiceBridge.Application.DTOs;

namespace InvoiceBridge.Web.Security;

internal sealed class LoggingNotificationDigestSender(ILogger<LoggingNotificationDigestSender> logger) : INotificationDigestSender
{
    public Task SendAsync(NotificationDigestMessage message, CancellationToken cancellationToken = default)
    {
        if (message.Recipients.Count == 0)
        {
            return Task.CompletedTask;
        }

        logger.LogInformation(
            "Notification digest generated at {CreatedAtUtc}. Recipients={Recipients}. Subject={Subject}. Body={Body}",
            message.CreatedAtUtc,
            string.Join(',', message.Recipients),
            message.Subject,
            message.Body);

        return Task.CompletedTask;
    }
}
