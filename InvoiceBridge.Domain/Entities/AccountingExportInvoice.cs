namespace InvoiceBridge.Domain.Entities;

public sealed class AccountingExportInvoice
{
    public int Id { get; set; }
    public int AccountingExportId { get; set; }
    public AccountingExport AccountingExport { get; set; } = null!;
    public int InvoiceId { get; set; }
    public Invoice Invoice { get; set; } = null!;
}
