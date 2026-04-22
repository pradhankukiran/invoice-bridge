using InvoiceBridge.Application.DTOs;
using InvoiceBridge.Application.Workflow;

namespace InvoiceBridge.Tests;

public sealed class WorkflowCalculatorTests
{
    [Fact]
    public void ApprovalSlaEvaluator_ReturnsOnTrack_Within24Hours()
    {
        var requestedAt = DateTimeOffset.UtcNow.AddHours(-12);
        var state = ApprovalSlaEvaluator.Evaluate(requestedAt, DateTimeOffset.UtcNow);

        Assert.Equal(ApprovalSlaState.OnTrack, state);
    }

    [Fact]
    public void ApprovalSlaEvaluator_ReturnsEscalating_After24Hours()
    {
        var requestedAt = DateTimeOffset.UtcNow.AddHours(-30);
        var state = ApprovalSlaEvaluator.Evaluate(requestedAt, DateTimeOffset.UtcNow);

        Assert.Equal(ApprovalSlaState.Escalating, state);
    }

    [Fact]
    public void ApprovalSlaEvaluator_ReturnsBreached_After48Hours()
    {
        var requestedAt = DateTimeOffset.UtcNow.AddHours(-60);
        var state = ApprovalSlaEvaluator.Evaluate(requestedAt, DateTimeOffset.UtcNow);

        Assert.Equal(ApprovalSlaState.Breached, state);
    }

    [Fact]
    public void PaymentBalanceCalculator_ComputesOutstandingAndPaidState()
    {
        var outstanding = PaymentBalanceCalculator.Outstanding(100m, 25m);
        var isPaid = PaymentBalanceCalculator.IsPaid(100m, 100m);

        Assert.Equal(75m, outstanding);
        Assert.True(isPaid);
    }
}
