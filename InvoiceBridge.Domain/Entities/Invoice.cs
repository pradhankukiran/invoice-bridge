using InvoiceBridge.Domain.Enums;

namespace InvoiceBridge.Domain.Entities;

public sealed class Invoice
{
    public int Id { get; set; }
    public required string InvoiceNumber { get; set; }
    public int SupplierId { get; set; }
    public Supplier Supplier { get; set; } = null!;
    public int? FileImportId { get; set; }
    public FileImport? FileImport { get; set; }
    public required string CurrencyCode { get; set; }
    public DateOnly InvoiceDate { get; set; }
    public DateOnly? DueDate { get; set; }
    public decimal Subtotal { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal TotalAmount { get; set; }
    public InvoiceStatus Status { get; set; } = InvoiceStatus.Imported;
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public ICollection<InvoiceLine> Lines { get; set; } = new List<InvoiceLine>();
    public ICollection<MatchResult> MatchResults { get; set; } = new List<MatchResult>();
    public ApprovalRequest? ApprovalRequest { get; set; }
    public ICollection<AccountingExportInvoice> AccountingExportInvoices { get; set; } = new List<AccountingExportInvoice>();
    public ICollection<Payment> Payments { get; set; } = new List<Payment>();
}
