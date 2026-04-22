using InvoiceBridge.Domain.Enums;

namespace InvoiceBridge.Application.DTOs;

public enum ApprovalSlaState
{
    OnTrack = 1,
    Escalating = 2,
    Breached = 3
}

public sealed record ApprovalQueueItemDto(
    int InvoiceId,
    string InvoiceNumber,
    string SupplierName,
    string AssignedRole,
    decimal TotalAmount,
    DateOnly InvoiceDate,
    DateTimeOffset RequestedAtUtc,
    double HoursPending,
    ApprovalSlaState SlaState);

public sealed record ApprovalActionHistoryDto(
    string Actor,
    ApprovalDecision Decision,
    string? Comment,
    DateTimeOffset ActionAtUtc);

public sealed class ApprovalDecisionRequest
{
    public int InvoiceId { get; set; }
    public ApprovalDecision Decision { get; set; }
    public string Actor { get; set; } = "manager.approver";
    public string? Comment { get; set; }
}

public sealed record ApprovalDecisionResultDto(
    int InvoiceId,
    InvoiceStatus NewStatus,
    string Message);
