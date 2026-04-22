namespace InvoiceBridge.Application.DTOs;

public sealed class AuditQueryRequest
{
    public string? EntityName { get; set; }
    public string? Action { get; set; }
    public string? Actor { get; set; }
    public DateTimeOffset? FromUtc { get; set; }
    public DateTimeOffset? ToUtc { get; set; }
    public int MaxRows { get; set; } = 250;
}

public sealed record AuditLogDto(
    int Id,
    string EntityName,
    string EntityId,
    string Action,
    string Actor,
    string Details,
    DateTimeOffset OccurredAtUtc);
