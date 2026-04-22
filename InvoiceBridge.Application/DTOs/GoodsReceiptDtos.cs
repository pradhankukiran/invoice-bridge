namespace InvoiceBridge.Application.DTOs;

public sealed class CreateGoodsReceiptRequest
{
    public int PurchaseOrderId { get; set; }
    public string GrnNumber { get; set; } = string.Empty;
    public string ReceivedBy { get; set; } = "warehouse.user";
    public List<GoodsReceiptLineInputDto> Lines { get; set; } = [];
}

public sealed class GoodsReceiptLineInputDto
{
    public int PurchaseOrderLineId { get; set; }
    public decimal ReceivedQuantity { get; set; }
    public decimal DamagedQuantity { get; set; }
}

public sealed record GoodsReceiptSummaryDto(
    int Id,
    string GrnNumber,
    string PoNumber,
    string ReceivedBy,
    DateTimeOffset ReceivedAtUtc,
    decimal TotalReceivedQuantity);
