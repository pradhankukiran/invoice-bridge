namespace InvoiceBridge.Domain.Entities;

public sealed class SupplierItemMapping
{
    public int Id { get; set; }
    public int SupplierMappingProfileId { get; set; }
    public SupplierMappingProfile SupplierMappingProfile { get; set; } = null!;
    public required string ExternalItemCode { get; set; }
    public required string InternalItemCode { get; set; }
    public string? OverrideDescription { get; set; }
    public decimal? OverrideTaxRate { get; set; }
    public bool IsActive { get; set; } = true;
    public required string UpdatedBy { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}
