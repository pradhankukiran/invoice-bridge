namespace InvoiceBridge.Domain.Entities;

public sealed class GoodsReceiptLine
{
    public int Id { get; set; }
    public int GoodsReceiptId { get; set; }
    public GoodsReceipt GoodsReceipt { get; set; } = null!;
    public int PurchaseOrderLineId { get; set; }
    public PurchaseOrderLine PurchaseOrderLine { get; set; } = null!;
    public decimal ReceivedQuantity { get; set; }
    public decimal DamagedQuantity { get; set; }
}
