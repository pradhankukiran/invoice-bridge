using System.Globalization;
using InvoiceBridge.Application.Abstractions.Persistence;
using InvoiceBridge.Application.Abstractions.Services;
using InvoiceBridge.Application.Common;
using InvoiceBridge.Application.DTOs;
using InvoiceBridge.Application.Workflow;
using InvoiceBridge.Domain.Entities;
using InvoiceBridge.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace InvoiceBridge.Application.Services;

internal sealed class ApprovalService(
    IApplicationDbContext dbContext,
    INotificationPublisher notificationPublisher) : IApprovalService
{
    public async Task<IReadOnlyList<ApprovalQueueItemDto>> ListPendingAsync(string role, CancellationToken cancellationToken = default)
    {
        var normalizedRole = string.IsNullOrWhiteSpace(role) ? null : role.Trim();

        var query = dbContext.ApprovalRequests
            .Include(request => request.Invoice)
            .ThenInclude(invoice => invoice.Supplier)
            .Where(request =>
                request.CurrentDecision == ApprovalDecision.Pending
                && request.Invoice.Status == InvoiceStatus.PendingApproval);

        if (!string.IsNullOrWhiteSpace(normalizedRole))
        {
            query = query.Where(request => request.AssignedRole == normalizedRole);
        }

        var requests = await query.ToListAsync(cancellationToken);
        var nowUtc = DateTimeOffset.UtcNow;
        var hasNotificationUpdates = false;

        foreach (var requestItem in requests)
        {
            var slaState = ApprovalSlaEvaluator.Evaluate(requestItem.RequestedAtUtc, nowUtc);

            if (slaState == ApprovalSlaState.Escalating && requestItem.EscalationNotifiedAtUtc is null)
            {
                await PublishSlaNotificationAsync(requestItem, slaState, cancellationToken);
                requestItem.EscalationNotifiedAtUtc = nowUtc;
                hasNotificationUpdates = true;
            }

            if (slaState == ApprovalSlaState.Breached && requestItem.BreachNotifiedAtUtc is null)
            {
                await PublishSlaNotificationAsync(requestItem, slaState, cancellationToken);
                requestItem.BreachNotifiedAtUtc = nowUtc;
                hasNotificationUpdates = true;
            }
        }

        if (hasNotificationUpdates)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return requests
            .Select(item => new ApprovalQueueItemDto(
                item.InvoiceId,
                item.Invoice.InvoiceNumber,
                item.Invoice.Supplier.LegalName,
                item.AssignedRole,
                item.Invoice.TotalAmount,
                item.Invoice.InvoiceDate,
                item.RequestedAtUtc,
                (nowUtc - item.RequestedAtUtc).TotalHours,
                ApprovalSlaEvaluator.Evaluate(item.RequestedAtUtc, nowUtc)))
            .OrderBy(item => item.HoursPending)
            .ToList();
    }

    public async Task<IReadOnlyList<ApprovalActionHistoryDto>> GetHistoryAsync(int invoiceId, CancellationToken cancellationToken = default)
    {
        var actions = await dbContext.ApprovalActions
            .AsNoTracking()
            .Include(action => action.ApprovalRequest)
            .Where(action => action.ApprovalRequest.InvoiceId == invoiceId)
            .Select(action => new ApprovalActionHistoryDto(
                action.Actor,
                action.Decision,
                action.Comment,
                action.ActionAtUtc))
            .ToListAsync(cancellationToken);

        return actions
            .OrderByDescending(action => action.ActionAtUtc)
            .ToList();
    }

    public async Task<ApprovalDecisionResultDto> DecideAsync(ApprovalDecisionRequest request, CancellationToken cancellationToken = default)
    {
        if (request.Decision == ApprovalDecision.Pending)
        {
            throw new ArgumentException("Choose Approve or Reject.", nameof(request.Decision));
        }

        var approvalRequest = await dbContext.ApprovalRequests
            .Include(ar => ar.Invoice)
            .Include(ar => ar.Actions)
            .SingleOrDefaultAsync(ar => ar.InvoiceId == request.InvoiceId, cancellationToken);

        if (approvalRequest is null)
        {
            throw new InvalidOperationException("Approval request was not found for this invoice.");
        }

        if (approvalRequest.CurrentDecision != ApprovalDecision.Pending)
        {
            throw new InvalidOperationException("This approval request is already closed.");
        }

        if (approvalRequest.Invoice.Status != InvoiceStatus.PendingApproval)
        {
            throw new InvalidOperationException("Invoice is not in pending approval state.");
        }

        approvalRequest.CurrentDecision = request.Decision;
        approvalRequest.CompletedAtUtc = DateTimeOffset.UtcNow;

        approvalRequest.Actions.Add(new ApprovalAction
        {
            Actor = request.Actor.Trim(),
            Decision = request.Decision,
            Comment = request.Comment
        });

        approvalRequest.Invoice.Status = request.Decision == ApprovalDecision.Approved
            ? InvoiceStatus.Approved
            : InvoiceStatus.Rejected;

        AuditTrailWriter.Add(
            dbContext,
            entityName: "Invoice",
            entityId: approvalRequest.InvoiceId.ToString(CultureInfo.InvariantCulture),
            action: request.Decision == ApprovalDecision.Approved ? "ApprovalApproved" : "ApprovalRejected",
            actor: request.Actor,
            details: string.IsNullOrWhiteSpace(request.Comment)
                ? "Approval decision captured."
                : request.Comment);

        await dbContext.SaveChangesAsync(cancellationToken);

        return new ApprovalDecisionResultDto(
            approvalRequest.InvoiceId,
            approvalRequest.Invoice.Status,
            request.Decision == ApprovalDecision.Approved
                ? "Invoice approved and moved to Approved state."
                : "Invoice rejected and moved to Rejected state.");
    }

    private async Task PublishSlaNotificationAsync(
        ApprovalRequest request,
        ApprovalSlaState slaState,
        CancellationToken cancellationToken)
    {
        var severity = slaState == ApprovalSlaState.Breached ? "Critical" : "Warning";
        var stateLabel = slaState == ApprovalSlaState.Breached ? "breached" : "escalating";

        await notificationPublisher.PublishToRoleAsync(
            request.AssignedRole,
            new NotificationPublishRequest
            {
                Category = "ApprovalSla",
                Severity = severity,
                Title = $"Approval SLA {stateLabel}: {request.Invoice.InvoiceNumber}",
                Message = $"Invoice {request.Invoice.InvoiceNumber} for {request.Invoice.Supplier.LegalName} requires attention. Current SLA state is {slaState}.",
                LinkUrl = "/approvals",
                SourceEntityName = "ApprovalRequest",
                SourceEntityId = request.Id.ToString(CultureInfo.InvariantCulture),
                Actor = "system.sla-monitor",
                SendDigest = true
            },
            cancellationToken);
    }
}
