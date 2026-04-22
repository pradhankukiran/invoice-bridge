namespace InvoiceBridge.Domain.Entities;

public sealed class Product
{
    public int Id { get; set; }
    public required string Sku { get; set; }
    public required string Name { get; set; }
    public decimal DefaultUnitPrice { get; set; }
    public bool IsActive { get; set; } = true;

    public ICollection<PurchaseOrderLine> PurchaseOrderLines { get; set; } = new List<PurchaseOrderLine>();
}
