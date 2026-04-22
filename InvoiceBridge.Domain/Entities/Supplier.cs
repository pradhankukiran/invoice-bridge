namespace InvoiceBridge.Domain.Entities;

public sealed class Supplier
{
    public int Id { get; set; }
    public required string SupplierCode { get; set; }
    public required string LegalName { get; set; }
    public string? Email { get; set; }
    public bool IsActive { get; set; } = true;

    public ICollection<PurchaseOrder> PurchaseOrders { get; set; } = new List<PurchaseOrder>();
    public ICollection<Invoice> Invoices { get; set; } = new List<Invoice>();
    public SupplierMappingProfile? MappingProfile { get; set; }
}
