using InvoiceBridge.Domain.Enums;

namespace InvoiceBridge.Domain.Entities;

public sealed class MatchResultLine
{
    public int Id { get; set; }
    public int MatchResultId { get; set; }
    public MatchResult MatchResult { get; set; } = null!;
    public int InvoiceLineId { get; set; }
    public InvoiceLine InvoiceLine { get; set; } = null!;
    public int? PurchaseOrderLineId { get; set; }
    public PurchaseOrderLine? PurchaseOrderLine { get; set; }
    public decimal QuantityVariance { get; set; }
    public decimal PriceVariance { get; set; }
    public decimal TaxVariance { get; set; }
    public MatchResultCode ResultCode { get; set; }
}
