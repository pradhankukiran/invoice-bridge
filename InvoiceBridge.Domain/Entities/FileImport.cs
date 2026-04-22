using InvoiceBridge.Domain.Enums;

namespace InvoiceBridge.Domain.Entities;

public sealed class FileImport
{
    public int Id { get; set; }
    public required string FileName { get; set; }
    public required string ImportedBy { get; set; }
    public required string XmlContent { get; set; }
    public string? XsdContent { get; set; }
    public DateTimeOffset ImportedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ProcessingStartedAtUtc { get; set; }
    public DateTimeOffset? ProcessedAtUtc { get; set; }
    public DateTimeOffset? NextRetryAtUtc { get; set; }
    public FileImportStatus Status { get; set; } = FileImportStatus.Queued;
    public int RetryCount { get; set; }
    public int ErrorCount { get; set; }
    public string? LastErrorMessage { get; set; }
    public uint RowVersion { get; set; }

    public ICollection<FileImportError> Errors { get; set; } = new List<FileImportError>();
    public ICollection<Invoice> Invoices { get; set; } = new List<Invoice>();
}
