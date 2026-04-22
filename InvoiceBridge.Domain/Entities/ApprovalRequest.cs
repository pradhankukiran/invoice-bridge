using InvoiceBridge.Domain.Enums;

namespace InvoiceBridge.Domain.Entities;

public sealed class ApprovalRequest
{
    public int Id { get; set; }
    public int InvoiceId { get; set; }
    public Invoice Invoice { get; set; } = null!;
    public required string AssignedRole { get; set; }
    public DateTimeOffset RequestedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public ApprovalDecision CurrentDecision { get; set; } = ApprovalDecision.Pending;
    public DateTimeOffset? CompletedAtUtc { get; set; }
    public DateTimeOffset? EscalationNotifiedAtUtc { get; set; }
    public DateTimeOffset? BreachNotifiedAtUtc { get; set; }
    public uint RowVersion { get; set; }

    public ICollection<ApprovalAction> Actions { get; set; } = new List<ApprovalAction>();
}
