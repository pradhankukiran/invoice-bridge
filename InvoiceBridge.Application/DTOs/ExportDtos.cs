namespace InvoiceBridge.Application.DTOs;

public sealed record ExportCandidateInvoiceDto(
    int InvoiceId,
    string InvoiceNumber,
    string SupplierCode,
    string SupplierName,
    DateOnly InvoiceDate,
    DateOnly? DueDate,
    string CurrencyCode,
    decimal Subtotal,
    decimal TaxAmount,
    decimal TotalAmount);

public sealed class CreateAccountingExportRequest
{
    public string GeneratedBy { get; set; } = "finance.officer";
    public string Format { get; set; } = "CSV";
    public List<int> InvoiceIds { get; set; } = [];
    public bool IncludeAllEligible { get; set; } = true;
}

public sealed class AccountingExportResultDto
{
    public int ExportId { get; init; }
    public string ExportReference { get; init; } = string.Empty;
    public string Format { get; init; } = string.Empty;
    public int InvoiceCount { get; init; }
    public decimal TotalAmount { get; init; }
    public DateTimeOffset GeneratedAtUtc { get; init; }
    public string Payload { get; init; } = string.Empty;
}

public sealed record AccountingExportSummaryDto(
    int ExportId,
    string ExportReference,
    string Format,
    string Status,
    int InvoiceCount,
    decimal TotalAmount,
    string GeneratedBy,
    DateTimeOffset GeneratedAtUtc);

public sealed class AccountingExportDownloadDto
{
    public string FileName { get; init; } = string.Empty;
    public string ContentType { get; init; } = "text/plain; charset=utf-8";
    public string Content { get; init; } = string.Empty;
}
