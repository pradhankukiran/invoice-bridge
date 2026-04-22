using InvoiceBridge.Application.DTOs;

namespace InvoiceBridge.Application.Abstractions.Services;

public interface IPurchaseOrderService
{
    Task<IReadOnlyList<SupplierLookupDto>> ListSuppliersAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PurchaseOrderSummaryDto>> ListAsync(CancellationToken cancellationToken = default);
    Task<PurchaseOrderDetailsDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<int> CreateAsync(CreatePurchaseOrderRequest request, CancellationToken cancellationToken = default);
}
