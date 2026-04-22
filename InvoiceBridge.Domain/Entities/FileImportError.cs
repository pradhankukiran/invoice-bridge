namespace InvoiceBridge.Domain.Entities;

public sealed class FileImportError
{
    public int Id { get; set; }
    public int FileImportId { get; set; }
    public FileImport FileImport { get; set; } = null!;
    public required string Path { get; set; }
    public required string Message { get; set; }
    public required string Severity { get; set; }
}
