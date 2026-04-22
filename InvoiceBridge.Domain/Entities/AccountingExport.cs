namespace InvoiceBridge.Domain.Entities;

public sealed class AccountingExport
{
    public int Id { get; set; }
    public required string ExportReference { get; set; }
    public required string Format { get; set; }
    public required string Status { get; set; }
    public required string GeneratedBy { get; set; }
    public DateTimeOffset GeneratedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public int InvoiceCount { get; set; }
    public decimal TotalAmount { get; set; }
    public required string Payload { get; set; }

    public ICollection<AccountingExportInvoice> ExportInvoices { get; set; } = new List<AccountingExportInvoice>();
}
