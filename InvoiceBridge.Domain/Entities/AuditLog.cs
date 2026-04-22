namespace InvoiceBridge.Domain.Entities;

public sealed class AuditLog
{
    public int Id { get; set; }
    public required string EntityName { get; set; }
    public required string EntityId { get; set; }
    public required string Action { get; set; }
    public required string Actor { get; set; }
    public required string Details { get; set; }
    public DateTimeOffset OccurredAtUtc { get; set; } = DateTimeOffset.UtcNow;
}
