namespace InvoiceBridge.Application.DTOs;

public sealed record UserNotificationDto(
    int NotificationId,
    string Category,
    string Severity,
    string Title,
    string Message,
    string? LinkUrl,
    bool IsRead,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? ReadAtUtc);

public sealed class NotificationListRequest
{
    public string RecipientUsername { get; set; } = string.Empty;
    public int MaxRows { get; set; } = 100;
    public bool IncludeRead { get; set; } = true;
}

public sealed class NotificationPublishRequest
{
    public IReadOnlyList<string> RecipientUsernames { get; set; } = [];
    public string Category { get; set; } = "General";
    public string Severity { get; set; } = "Info";
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? LinkUrl { get; set; }
    public string? SourceEntityName { get; set; }
    public string? SourceEntityId { get; set; }
    public string Actor { get; set; } = "system";
    public bool SendDigest { get; set; }
}

public sealed class NotificationDigestMessage
{
    public IReadOnlyList<string> Recipients { get; init; } = [];
    public string Subject { get; init; } = string.Empty;
    public string Body { get; init; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; init; } = DateTimeOffset.UtcNow;
}
