using InvoiceBridge.Application.Abstractions.Services;
using InvoiceBridge.Application.DTOs;
using InvoiceBridge.Domain.Entities;
using InvoiceBridge.Domain.Enums;
using InvoiceBridge.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace InvoiceBridge.Tests;

public sealed class ApprovalWorkflowIntegrationTests
{
    [Fact]
    public async Task ListPendingAsync_WhenSlaEscalatesOrBreaches_PublishesNotificationsAndPersistsFlags()
    {
        await using var testScope = await IntegrationTestScope.CreateAsync();
        using var scope = testScope.CreateScope();

        var dbContext = scope.ServiceProvider.GetRequiredService<InvoiceBridgeDbContext>();
        var approvalService = scope.ServiceProvider.GetRequiredService<IApprovalService>();
        var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();

        await CreatePendingApprovalAsync(
            dbContext,
            invoiceNumber: "INV-SLA-ESC-001",
            requestedAtUtc: DateTimeOffset.UtcNow.AddHours(-30));

        await CreatePendingApprovalAsync(
            dbContext,
            invoiceNumber: "INV-SLA-BRE-001",
            requestedAtUtc: DateTimeOffset.UtcNow.AddHours(-55));

        var queue = await approvalService.ListPendingAsync("Manager");

        Assert.Equal(2, queue.Count);
        Assert.Contains(queue, item => item.InvoiceNumber == "INV-SLA-ESC-001" && item.SlaState == ApprovalSlaState.Escalating);
        Assert.Contains(queue, item => item.InvoiceNumber == "INV-SLA-BRE-001" && item.SlaState == ApprovalSlaState.Breached);

        var approvalRequests = await dbContext.ApprovalRequests
            .Include(item => item.Invoice)
            .ToListAsync();

        var escalationRequest = approvalRequests.Single(item => item.Invoice.InvoiceNumber == "INV-SLA-ESC-001");
        var breachedRequest = approvalRequests.Single(item => item.Invoice.InvoiceNumber == "INV-SLA-BRE-001");

        Assert.NotNull(escalationRequest.EscalationNotifiedAtUtc);
        Assert.Null(escalationRequest.BreachNotifiedAtUtc);

        Assert.NotNull(breachedRequest.BreachNotifiedAtUtc);

        var managerNotifications = await notificationService.ListForUserAsync(new NotificationListRequest
        {
            RecipientUsername = "manager.approver",
            IncludeRead = true,
            MaxRows = 50
        });

        Assert.Contains(managerNotifications, item => item.Category == "ApprovalSla" && item.Severity == "Warning");
        Assert.Contains(managerNotifications, item => item.Category == "ApprovalSla" && item.Severity == "Critical");

        Assert.True(testScope.DigestSender.Messages.Count >= 2);
    }

    [Fact]
    public async Task DecideAsync_TransitionsInvoiceAndPreventsDoubleDecision()
    {
        await using var testScope = await IntegrationTestScope.CreateAsync();
        using var scope = testScope.CreateScope();

        var dbContext = scope.ServiceProvider.GetRequiredService<InvoiceBridgeDbContext>();
        var approvalService = scope.ServiceProvider.GetRequiredService<IApprovalService>();

        var approvedInvoiceId = await CreatePendingApprovalAsync(
            dbContext,
            invoiceNumber: "INV-APPROVE-001",
            requestedAtUtc: DateTimeOffset.UtcNow.AddHours(-4));

        var rejectedInvoiceId = await CreatePendingApprovalAsync(
            dbContext,
            invoiceNumber: "INV-REJECT-001",
            requestedAtUtc: DateTimeOffset.UtcNow.AddHours(-2));

        var approveResult = await approvalService.DecideAsync(new ApprovalDecisionRequest
        {
            InvoiceId = approvedInvoiceId,
            Decision = ApprovalDecision.Approved,
            Actor = "manager.approver",
            Comment = "Approved in integration test."
        });

        Assert.Equal(InvoiceStatus.Approved, approveResult.NewStatus);

        var approvedInvoice = await dbContext.Invoices.SingleAsync(item => item.Id == approvedInvoiceId);
        var approvedRequest = await dbContext.ApprovalRequests
            .Include(item => item.Actions)
            .SingleAsync(item => item.InvoiceId == approvedInvoiceId);

        Assert.Equal(InvoiceStatus.Approved, approvedInvoice.Status);
        Assert.Equal(ApprovalDecision.Approved, approvedRequest.CurrentDecision);
        Assert.NotNull(approvedRequest.CompletedAtUtc);
        Assert.Single(approvedRequest.Actions);

        await Assert.ThrowsAsync<InvalidOperationException>(() => approvalService.DecideAsync(new ApprovalDecisionRequest
        {
            InvoiceId = approvedInvoiceId,
            Decision = ApprovalDecision.Rejected,
            Actor = "manager.approver",
            Comment = "Second decision should fail."
        }));

        var rejectResult = await approvalService.DecideAsync(new ApprovalDecisionRequest
        {
            InvoiceId = rejectedInvoiceId,
            Decision = ApprovalDecision.Rejected,
            Actor = "manager.approver",
            Comment = "Rejected in integration test."
        });

        Assert.Equal(InvoiceStatus.Rejected, rejectResult.NewStatus);

        var rejectedInvoice = await dbContext.Invoices.SingleAsync(item => item.Id == rejectedInvoiceId);
        Assert.Equal(InvoiceStatus.Rejected, rejectedInvoice.Status);
    }

    private static async Task<int> CreatePendingApprovalAsync(
        InvoiceBridgeDbContext dbContext,
        string invoiceNumber,
        DateTimeOffset requestedAtUtc)
    {
        var supplier = new Supplier
        {
            SupplierCode = $"SUP-{invoiceNumber}",
            LegalName = $"Supplier {invoiceNumber}",
            Email = "supplier@example.test",
            IsActive = true
        };

        var invoice = new Invoice
        {
            InvoiceNumber = invoiceNumber,
            Supplier = supplier,
            CurrencyCode = "USD",
            InvoiceDate = DateOnly.FromDateTime(DateTime.UtcNow),
            Subtotal = 100m,
            TaxAmount = 5m,
            TotalAmount = 105m,
            Status = InvoiceStatus.PendingApproval,
            Lines =
            [
                new InvoiceLine
                {
                    LineNumber = 1,
                    ItemCode = "ITEM-APP-1",
                    Description = "Approval Integration Item",
                    BilledQuantity = 1m,
                    UnitPrice = 100m,
                    TaxRate = 5m,
                    LineTotal = 100m
                }
            ],
            ApprovalRequest = new ApprovalRequest
            {
                AssignedRole = "Manager",
                RequestedAtUtc = requestedAtUtc,
                CurrentDecision = ApprovalDecision.Pending
            }
        };

        dbContext.Invoices.Add(invoice);
        await dbContext.SaveChangesAsync();

        return invoice.Id;
    }
}
