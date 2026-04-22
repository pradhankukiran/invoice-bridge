using InvoiceBridge.Domain.Enums;

namespace InvoiceBridge.Application.DTOs;

public sealed record PayableInvoiceDto(
    int InvoiceId,
    string InvoiceNumber,
    string SupplierName,
    DateOnly InvoiceDate,
    DateOnly? DueDate,
    string CurrencyCode,
    decimal TotalAmount,
    decimal PaidAmount,
    decimal OutstandingAmount,
    InvoiceStatus Status);

public sealed class RecordPaymentRequest
{
    public int InvoiceId { get; set; }
    public decimal Amount { get; set; }
    public DateOnly PaymentDate { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow);
    public string Method { get; set; } = "BankTransfer";
    public string ReferenceNumber { get; set; } = string.Empty;
    public string RecordedBy { get; set; } = "finance.officer";
}

public sealed record PaymentLedgerItemDto(
    int PaymentId,
    int InvoiceId,
    string InvoiceNumber,
    string SupplierName,
    decimal Amount,
    DateOnly PaymentDate,
    string Method,
    string ReferenceNumber,
    string RecordedBy,
    DateTimeOffset RecordedAtUtc);
