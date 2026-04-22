namespace InvoiceBridge.Domain.Entities;

public sealed class SupplierMappingProfile
{
    public int Id { get; set; }
    public int SupplierId { get; set; }
    public Supplier Supplier { get; set; } = null!;
    public bool IsActive { get; set; } = true;
    public bool RequireMappedItems { get; set; }
    public decimal? DefaultTaxRate { get; set; }
    public required string UpdatedBy { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public ICollection<SupplierItemMapping> ItemMappings { get; set; } = new List<SupplierItemMapping>();
}
