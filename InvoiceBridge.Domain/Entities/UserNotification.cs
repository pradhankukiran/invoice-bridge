namespace InvoiceBridge.Domain.Entities;

public sealed class UserNotification
{
    public int Id { get; set; }
    public required string RecipientUsername { get; set; }
    public required string Category { get; set; }
    public required string Severity { get; set; }
    public required string Title { get; set; }
    public required string Message { get; set; }
    public string? LinkUrl { get; set; }
    public string? SourceEntityName { get; set; }
    public string? SourceEntityId { get; set; }
    public bool IsRead { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ReadAtUtc { get; set; }
}
