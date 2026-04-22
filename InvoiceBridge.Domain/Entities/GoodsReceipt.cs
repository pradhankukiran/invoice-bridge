namespace InvoiceBridge.Domain.Entities;

public sealed class GoodsReceipt
{
    public int Id { get; set; }
    public required string GrnNumber { get; set; }
    public int PurchaseOrderId { get; set; }
    public PurchaseOrder PurchaseOrder { get; set; } = null!;
    public required string ReceivedBy { get; set; }
    public DateTimeOffset ReceivedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public ICollection<GoodsReceiptLine> Lines { get; set; } = new List<GoodsReceiptLine>();
}
