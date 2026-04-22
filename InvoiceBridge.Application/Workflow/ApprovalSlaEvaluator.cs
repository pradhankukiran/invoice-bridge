using InvoiceBridge.Application.DTOs;

namespace InvoiceBridge.Application.Workflow;

public static class ApprovalSlaEvaluator
{
    public static ApprovalSlaState Evaluate(DateTimeOffset requestedAtUtc, DateTimeOffset nowUtc)
    {
        var hoursPending = (nowUtc - requestedAtUtc).TotalHours;
        if (hoursPending > 48)
        {
            return ApprovalSlaState.Breached;
        }

        if (hoursPending > 24)
        {
            return ApprovalSlaState.Escalating;
        }

        return ApprovalSlaState.OnTrack;
    }
}
