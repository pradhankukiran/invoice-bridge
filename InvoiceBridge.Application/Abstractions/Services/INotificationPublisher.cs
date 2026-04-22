using InvoiceBridge.Application.DTOs;

namespace InvoiceBridge.Application.Abstractions.Services;

public interface INotificationPublisher
{
    Task<int> PublishAsync(NotificationPublishRequest request, CancellationToken cancellationToken = default);
    Task<int> PublishToRoleAsync(string role, NotificationPublishRequest request, CancellationToken cancellationToken = default);
}
