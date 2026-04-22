using InvoiceBridge.Application.DTOs;

namespace InvoiceBridge.Application.Abstractions.Services;

public interface IApprovalService
{
    Task<IReadOnlyList<ApprovalQueueItemDto>> ListPendingAsync(string role, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ApprovalActionHistoryDto>> GetHistoryAsync(int invoiceId, CancellationToken cancellationToken = default);
    Task<ApprovalDecisionResultDto> DecideAsync(ApprovalDecisionRequest request, CancellationToken cancellationToken = default);
}
