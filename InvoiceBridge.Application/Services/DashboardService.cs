using InvoiceBridge.Application.Abstractions.Persistence;
using InvoiceBridge.Application.Abstractions.Services;
using InvoiceBridge.Application.DTOs;
using InvoiceBridge.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace InvoiceBridge.Application.Services;

internal sealed class DashboardService(IApplicationDbContext dbContext) : IDashboardService
{
    public async Task<DashboardMetricsDto> GetMetricsAsync(CancellationToken cancellationToken = default)
    {
        var sevenDaysAgo = DateTimeOffset.UtcNow.AddDays(-7);

        var openPos = await dbContext.PurchaseOrders.CountAsync(
            po => po.Status == PurchaseOrderStatus.Submitted || po.Status == PurchaseOrderStatus.PartiallyReceived,
            cancellationToken);

        var importedInvoices = await dbContext.Invoices.CountAsync(invoice => invoice.Status == InvoiceStatus.Imported, cancellationToken);
        var pendingApprovals = await dbContext.Invoices.CountAsync(invoice => invoice.Status == InvoiceStatus.PendingApproval, cancellationToken);
        var exceptions = await dbContext.Invoices.CountAsync(invoice => invoice.Status == InvoiceStatus.Exception, cancellationToken);
        var failedImports = await dbContext.FileImports.CountAsync(file => file.Status == FileImportStatus.Failed, cancellationToken);
        var matchedResults = await dbContext.MatchResults
            .Where(result => result.IsMatch)
            .Select(result => result.ExecutedAtUtc)
            .ToListAsync(cancellationToken);

        var matchedLast7Days = matchedResults.Count(executedAt => executedAt >= sevenDaysAgo);

        return new DashboardMetricsDto(openPos, importedInvoices, pendingApprovals, exceptions, failedImports, matchedLast7Days);
    }
}
