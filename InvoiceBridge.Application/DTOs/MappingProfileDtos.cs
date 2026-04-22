namespace InvoiceBridge.Application.DTOs;

public sealed record SupplierMappingProfileDto(
    int ProfileId,
    int SupplierId,
    string SupplierCode,
    string SupplierName,
    bool IsActive,
    bool RequireMappedItems,
    decimal? DefaultTaxRate,
    string UpdatedBy,
    DateTimeOffset UpdatedAtUtc,
    int MappingCount);

public sealed class UpsertSupplierMappingProfileRequest
{
    public int? ProfileId { get; set; }
    public int SupplierId { get; set; }
    public bool IsActive { get; set; } = true;
    public bool RequireMappedItems { get; set; }
    public decimal? DefaultTaxRate { get; set; }
    public string UpdatedBy { get; set; } = "integration.admin";
}

public sealed record SupplierItemMappingDto(
    int MappingId,
    int ProfileId,
    string ExternalItemCode,
    string InternalItemCode,
    string? OverrideDescription,
    decimal? OverrideTaxRate,
    bool IsActive,
    string UpdatedBy,
    DateTimeOffset UpdatedAtUtc);

public sealed class UpsertSupplierItemMappingRequest
{
    public int ProfileId { get; set; }
    public int? MappingId { get; set; }
    public string ExternalItemCode { get; set; } = string.Empty;
    public string InternalItemCode { get; set; } = string.Empty;
    public string? OverrideDescription { get; set; }
    public decimal? OverrideTaxRate { get; set; }
    public bool IsActive { get; set; } = true;
    public string UpdatedBy { get; set; } = "integration.admin";
}
