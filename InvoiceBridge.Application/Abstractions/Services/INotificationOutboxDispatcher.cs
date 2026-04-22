namespace InvoiceBridge.Application.Abstractions.Services;

public interface INotificationOutboxDispatcher
{
    Task<int> DispatchPendingAsync(int maxBatch, CancellationToken cancellationToken = default);
}
