using InvoiceBridge.Domain.Enums;

namespace InvoiceBridge.Application.DTOs;

public sealed class CreatePurchaseOrderRequest
{
    public string? PoNumber { get; set; }
    public int SupplierId { get; set; }
    public string CurrencyCode { get; set; } = "USD";
    public DateOnly? ExpectedDeliveryDate { get; set; }
    public string CreatedBy { get; set; } = "system";
    public List<PurchaseOrderLineInputDto> Lines { get; set; } = [];
}

public sealed class PurchaseOrderLineInputDto
{
    public string ItemCode { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal OrderedQuantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal TaxRate { get; set; }
}

public sealed record PurchaseOrderSummaryDto(
    int Id,
    string PoNumber,
    string SupplierName,
    PurchaseOrderStatus Status,
    decimal TotalAmount,
    DateTimeOffset CreatedAtUtc);

public sealed record PurchaseOrderLineDto(
    int Id,
    string ItemCode,
    string Description,
    decimal OrderedQuantity,
    decimal UnitPrice,
    decimal TaxRate);

public sealed record PurchaseOrderDetailsDto(
    int Id,
    string PoNumber,
    int SupplierId,
    string SupplierName,
    string CurrencyCode,
    PurchaseOrderStatus Status,
    DateOnly? ExpectedDeliveryDate,
    IReadOnlyList<PurchaseOrderLineDto> Lines);

public sealed record OpenPurchaseOrderDto(
    int Id,
    string PoNumber,
    string SupplierName,
    string CurrencyCode,
    IReadOnlyList<PurchaseOrderLineDto> Lines);
