using InvoiceBridge.Domain.Enums;

namespace InvoiceBridge.Application.DTOs;

public sealed record ExceptionInvoiceDto(
    int InvoiceId,
    string InvoiceNumber,
    string SupplierName,
    DateOnly InvoiceDate,
    decimal TotalAmount,
    MatchResultCode? LastResultCode,
    DateTimeOffset? LastMatchedAtUtc);

public sealed class ExceptionResolutionRequest
{
    public int InvoiceId { get; set; }
    public string Actor { get; set; } = "ap.accountant";
    public string Note { get; set; } = string.Empty;
    public bool RerunMatch { get; set; } = true;
}

public sealed class ExceptionResolutionResultDto
{
    public int InvoiceId { get; init; }
    public InvoiceStatus InvoiceStatus { get; init; }
    public string Message { get; init; } = string.Empty;
    public MatchRunResultDto? MatchResult { get; init; }
}
