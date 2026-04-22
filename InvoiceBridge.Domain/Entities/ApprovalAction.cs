using InvoiceBridge.Domain.Enums;

namespace InvoiceBridge.Domain.Entities;

public sealed class ApprovalAction
{
    public int Id { get; set; }
    public int ApprovalRequestId { get; set; }
    public ApprovalRequest ApprovalRequest { get; set; } = null!;
    public required string Actor { get; set; }
    public ApprovalDecision Decision { get; set; }
    public string? Comment { get; set; }
    public DateTimeOffset ActionAtUtc { get; set; } = DateTimeOffset.UtcNow;
}
