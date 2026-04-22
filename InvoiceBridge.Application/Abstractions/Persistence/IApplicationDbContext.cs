using InvoiceBridge.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace InvoiceBridge.Application.Abstractions.Persistence;

public interface IApplicationDbContext
{
    DbSet<Supplier> Suppliers { get; }
    DbSet<Product> Products { get; }
    DbSet<PurchaseOrder> PurchaseOrders { get; }
    DbSet<PurchaseOrderLine> PurchaseOrderLines { get; }
    DbSet<GoodsReceipt> GoodsReceipts { get; }
    DbSet<GoodsReceiptLine> GoodsReceiptLines { get; }
    DbSet<Invoice> Invoices { get; }
    DbSet<InvoiceLine> InvoiceLines { get; }
    DbSet<FileImport> FileImports { get; }
    DbSet<FileImportError> FileImportErrors { get; }
    DbSet<MatchResult> MatchResults { get; }
    DbSet<MatchResultLine> MatchResultLines { get; }
    DbSet<ApprovalRequest> ApprovalRequests { get; }
    DbSet<ApprovalAction> ApprovalActions { get; }
    DbSet<AccountingExport> AccountingExports { get; }
    DbSet<AccountingExportInvoice> AccountingExportInvoices { get; }
    DbSet<Payment> Payments { get; }
    DbSet<AuditLog> AuditLogs { get; }
    DbSet<SupplierMappingProfile> SupplierMappingProfiles { get; }
    DbSet<SupplierItemMapping> SupplierItemMappings { get; }
    DbSet<UserNotification> UserNotifications { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
