using InvoiceBridge.Application.DTOs;

namespace InvoiceBridge.Application.Abstractions.Services;

public interface IGoodsReceiptService
{
    Task<IReadOnlyList<OpenPurchaseOrderDto>> ListOpenPurchaseOrdersAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<GoodsReceiptSummaryDto>> ListAsync(CancellationToken cancellationToken = default);
    Task<int> CreateAsync(CreateGoodsReceiptRequest request, CancellationToken cancellationToken = default);
}
