namespace InvoiceBridge.Domain.Entities;

public sealed class NotificationOutboxMessage
{
    public int Id { get; set; }
    public required string RecipientsJson { get; set; }
    public required string Subject { get; set; }
    public required string Body { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? DispatchedAtUtc { get; set; }
    public int AttemptCount { get; set; }
    public DateTimeOffset? NextAttemptAtUtc { get; set; }
    public string? LastError { get; set; }
}
