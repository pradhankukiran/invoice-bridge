using InvoiceBridge.Application.Abstractions.Services;
using InvoiceBridge.Application.DTOs;

namespace InvoiceBridge.Application.Services;

internal sealed class NullRoleRecipientResolver : IRoleRecipientResolver
{
    public Task<IReadOnlyList<string>> ResolveUsersByRoleAsync(string role, CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyList<string>>([]);
    }
}

internal sealed class NullNotificationDigestSender : INotificationDigestSender
{
    public Task SendAsync(NotificationDigestMessage message, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}
