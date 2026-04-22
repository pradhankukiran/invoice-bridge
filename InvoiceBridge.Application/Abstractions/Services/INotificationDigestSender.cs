using InvoiceBridge.Application.DTOs;

namespace InvoiceBridge.Application.Abstractions.Services;

public interface INotificationDigestSender
{
    Task SendAsync(NotificationDigestMessage message, CancellationToken cancellationToken = default);
}
