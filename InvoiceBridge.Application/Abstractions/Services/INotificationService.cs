using InvoiceBridge.Application.DTOs;

namespace InvoiceBridge.Application.Abstractions.Services;

public interface INotificationService
{
    Task<int> GetUnreadCountAsync(string recipientUsername, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<UserNotificationDto>> ListForUserAsync(NotificationListRequest request, CancellationToken cancellationToken = default);
    Task MarkAsReadAsync(int notificationId, string recipientUsername, CancellationToken cancellationToken = default);
    Task MarkAllAsReadAsync(string recipientUsername, CancellationToken cancellationToken = default);
}
