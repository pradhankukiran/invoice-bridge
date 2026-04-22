using InvoiceBridge.Domain.Enums;

namespace InvoiceBridge.Application.DTOs;

public sealed class InvoiceImportRequest
{
    public string FileName { get; set; } = string.Empty;
    public string XmlContent { get; set; } = string.Empty;
    public string? XsdContent { get; set; }
    public string ImportedBy { get; set; } = "ap.user";
}

public sealed class InvoiceImportResultDto
{
    public bool IsSuccess { get; init; }
    public int FileImportId { get; init; }
    public int? InvoiceId { get; init; }
    public string Message { get; init; } = string.Empty;
    public FileImportStatus FileImportStatus { get; init; }
    public IReadOnlyList<string> Errors { get; init; } = [];
}

public sealed class ProcessImportQueueRequest
{
    public int BatchSize { get; set; } = 10;
    public string ProcessedBy { get; set; } = "import.worker";
}

public sealed class ProcessImportQueueResultDto
{
    public int RequestedBatchSize { get; init; }
    public int ProcessedCount { get; init; }
    public int SucceededCount { get; init; }
    public int FailedCount { get; init; }
    public IReadOnlyList<InvoiceImportResultDto> Results { get; init; } = [];
}

public sealed class RetryFileImportRequest
{
    public int FileImportId { get; set; }
    public string RequestedBy { get; set; } = "ap.accountant";
    public int DelaySeconds { get; set; }
}

public sealed class RetryFileImportResultDto
{
    public int FileImportId { get; init; }
    public FileImportStatus Status { get; init; }
    public string Message { get; init; } = string.Empty;
}

public sealed record FileImportSummaryDto(
    int FileImportId,
    string FileName,
    string ImportedBy,
    DateTimeOffset ImportedAtUtc,
    DateTimeOffset? ProcessingStartedAtUtc,
    DateTimeOffset? ProcessedAtUtc,
    DateTimeOffset? NextRetryAtUtc,
    FileImportStatus Status,
    int RetryCount,
    int ErrorCount,
    int InvoiceCount,
    string? LastErrorMessage);

public sealed record FileImportDiagnosticsDto(
    int ErrorId,
    string Path,
    string Message,
    string Severity);

public sealed record FileImportInvoiceDto(
    int InvoiceId,
    string InvoiceNumber,
    string SupplierName,
    InvoiceStatus Status,
    decimal TotalAmount);

public sealed class FileImportDetailsDto
{
    public int FileImportId { get; init; }
    public string FileName { get; init; } = string.Empty;
    public string ImportedBy { get; init; } = string.Empty;
    public DateTimeOffset ImportedAtUtc { get; init; }
    public DateTimeOffset? ProcessingStartedAtUtc { get; init; }
    public DateTimeOffset? ProcessedAtUtc { get; init; }
    public DateTimeOffset? NextRetryAtUtc { get; init; }
    public FileImportStatus Status { get; init; }
    public int RetryCount { get; init; }
    public int ErrorCount { get; init; }
    public string? LastErrorMessage { get; init; }
    public IReadOnlyList<FileImportDiagnosticsDto> Diagnostics { get; init; } = [];
    public IReadOnlyList<FileImportInvoiceDto> ImportedInvoices { get; init; } = [];
}

public sealed record InvoiceSummaryDto(
    int Id,
    string InvoiceNumber,
    string SupplierName,
    InvoiceStatus Status,
    DateOnly InvoiceDate,
    decimal TotalAmount,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? LastMatchedAtUtc);

public sealed record MatchLineResultDto(
    int InvoiceLineId,
    string ItemCode,
    MatchResultCode ResultCode,
    decimal QuantityVariance,
    decimal PriceVariance,
    decimal TaxVariance);

public sealed class MatchRunResultDto
{
    public int MatchResultId { get; init; }
    public bool IsMatch { get; init; }
    public MatchResultCode ResultCode { get; init; }
    public IReadOnlyList<MatchLineResultDto> Lines { get; init; } = [];
}
