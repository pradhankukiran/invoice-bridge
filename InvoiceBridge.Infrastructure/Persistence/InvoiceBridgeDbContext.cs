using InvoiceBridge.Application.Abstractions.Persistence;
using InvoiceBridge.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace InvoiceBridge.Infrastructure.Persistence;

public sealed class InvoiceBridgeDbContext(DbContextOptions<InvoiceBridgeDbContext> options)
    : DbContext(options), IApplicationDbContext
{
    public DbSet<Supplier> Suppliers => Set<Supplier>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<PurchaseOrder> PurchaseOrders => Set<PurchaseOrder>();
    public DbSet<PurchaseOrderLine> PurchaseOrderLines => Set<PurchaseOrderLine>();
    public DbSet<GoodsReceipt> GoodsReceipts => Set<GoodsReceipt>();
    public DbSet<GoodsReceiptLine> GoodsReceiptLines => Set<GoodsReceiptLine>();
    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<InvoiceLine> InvoiceLines => Set<InvoiceLine>();
    public DbSet<FileImport> FileImports => Set<FileImport>();
    public DbSet<FileImportError> FileImportErrors => Set<FileImportError>();
    public DbSet<MatchResult> MatchResults => Set<MatchResult>();
    public DbSet<MatchResultLine> MatchResultLines => Set<MatchResultLine>();
    public DbSet<ApprovalRequest> ApprovalRequests => Set<ApprovalRequest>();
    public DbSet<ApprovalAction> ApprovalActions => Set<ApprovalAction>();
    public DbSet<AccountingExport> AccountingExports => Set<AccountingExport>();
    public DbSet<AccountingExportInvoice> AccountingExportInvoices => Set<AccountingExportInvoice>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<SupplierMappingProfile> SupplierMappingProfiles => Set<SupplierMappingProfile>();
    public DbSet<SupplierItemMapping> SupplierItemMappings => Set<SupplierItemMapping>();
    public DbSet<UserNotification> UserNotifications => Set<UserNotification>();
    public DbSet<NotificationOutboxMessage> NotificationOutbox => Set<NotificationOutboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Supplier>(entity =>
        {
            entity.Property(x => x.SupplierCode).HasMaxLength(64);
            entity.Property(x => x.LegalName).HasMaxLength(200);
            entity.HasIndex(x => x.SupplierCode).IsUnique();
        });

        modelBuilder.Entity<Product>(entity =>
        {
            entity.Property(x => x.Sku).HasMaxLength(64);
            entity.Property(x => x.Name).HasMaxLength(200);
            entity.Property(x => x.DefaultUnitPrice).HasPrecision(18, 2);
            entity.HasIndex(x => x.Sku).IsUnique();
        });

        modelBuilder.Entity<PurchaseOrder>(entity =>
        {
            entity.Property(x => x.PoNumber).HasMaxLength(64);
            entity.Property(x => x.CurrencyCode).HasMaxLength(3);
            entity.Property(x => x.CreatedBy).HasMaxLength(128);
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(32);
            entity.HasIndex(x => x.PoNumber).IsUnique();
            entity.HasOne(x => x.Supplier)
                .WithMany(x => x.PurchaseOrders)
                .HasForeignKey(x => x.SupplierId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<PurchaseOrderLine>(entity =>
        {
            entity.Property(x => x.ItemCode).HasMaxLength(64);
            entity.Property(x => x.Description).HasMaxLength(300);
            entity.Property(x => x.OrderedQuantity).HasPrecision(18, 2);
            entity.Property(x => x.UnitPrice).HasPrecision(18, 2);
            entity.Property(x => x.TaxRate).HasPrecision(9, 4);
        });

        modelBuilder.Entity<GoodsReceipt>(entity =>
        {
            entity.Property(x => x.GrnNumber).HasMaxLength(64);
            entity.Property(x => x.ReceivedBy).HasMaxLength(128);
            entity.HasIndex(x => x.GrnNumber).IsUnique();
            entity.HasOne(x => x.PurchaseOrder)
                .WithMany(x => x.GoodsReceipts)
                .HasForeignKey(x => x.PurchaseOrderId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<GoodsReceiptLine>(entity =>
        {
            entity.Property(x => x.ReceivedQuantity).HasPrecision(18, 2);
            entity.Property(x => x.DamagedQuantity).HasPrecision(18, 2);
            entity.HasOne(x => x.PurchaseOrderLine)
                .WithMany(x => x.GoodsReceiptLines)
                .HasForeignKey(x => x.PurchaseOrderLineId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Invoice>(entity =>
        {
            entity.Property(x => x.InvoiceNumber).HasMaxLength(64);
            entity.Property(x => x.CurrencyCode).HasMaxLength(3);
            entity.Property(x => x.Subtotal).HasPrecision(18, 2);
            entity.Property(x => x.TaxAmount).HasPrecision(18, 2);
            entity.Property(x => x.TotalAmount).HasPrecision(18, 2);
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(32);
            entity.Property(x => x.RowVersion).IsConcurrencyToken();
            entity.HasIndex(x => new { x.SupplierId, x.InvoiceNumber }).IsUnique();
            entity.HasOne(x => x.Supplier)
                .WithMany(x => x.Invoices)
                .HasForeignKey(x => x.SupplierId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<InvoiceLine>(entity =>
        {
            entity.Property(x => x.ItemCode).HasMaxLength(64);
            entity.Property(x => x.Description).HasMaxLength(300);
            entity.Property(x => x.BilledQuantity).HasPrecision(18, 2);
            entity.Property(x => x.UnitPrice).HasPrecision(18, 2);
            entity.Property(x => x.TaxRate).HasPrecision(9, 4);
            entity.Property(x => x.LineTotal).HasPrecision(18, 2);
        });

        modelBuilder.Entity<FileImport>(entity =>
        {
            entity.Property(x => x.FileName).HasMaxLength(260);
            entity.Property(x => x.ImportedBy).HasMaxLength(128);
            entity.Property(x => x.XmlContent).HasMaxLength(2_000_000);
            entity.Property(x => x.XsdContent).HasMaxLength(2_000_000);
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(32);
            entity.Property(x => x.LastErrorMessage).HasMaxLength(1000);
            entity.Property(x => x.RowVersion).IsConcurrencyToken();
            entity.HasIndex(x => new { x.Status, x.ImportedAtUtc });
            entity.HasIndex(x => x.NextRetryAtUtc);
        });

        modelBuilder.Entity<FileImportError>(entity =>
        {
            entity.Property(x => x.Path).HasMaxLength(256);
            entity.Property(x => x.Message).HasMaxLength(1000);
            entity.Property(x => x.Severity).HasMaxLength(16);
        });

        modelBuilder.Entity<MatchResult>(entity =>
        {
            entity.Property(x => x.ResultCode).HasConversion<string>().HasMaxLength(64);
            entity.Property(x => x.ExecutedBy).HasMaxLength(128);
            entity.Property(x => x.Notes).HasMaxLength(500);
        });

        modelBuilder.Entity<MatchResultLine>(entity =>
        {
            entity.Property(x => x.QuantityVariance).HasPrecision(18, 2);
            entity.Property(x => x.PriceVariance).HasPrecision(18, 2);
            entity.Property(x => x.TaxVariance).HasPrecision(9, 4);
            entity.Property(x => x.ResultCode).HasConversion<string>().HasMaxLength(64);
            entity.HasOne(x => x.PurchaseOrderLine)
                .WithMany(x => x.MatchResultLines)
                .HasForeignKey(x => x.PurchaseOrderLineId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ApprovalRequest>(entity =>
        {
            entity.Property(x => x.AssignedRole).HasMaxLength(64);
            entity.Property(x => x.CurrentDecision).HasConversion<string>().HasMaxLength(32);
            entity.Property(x => x.RowVersion).IsConcurrencyToken();
            entity.HasOne(x => x.Invoice)
                .WithOne(x => x.ApprovalRequest)
                .HasForeignKey<ApprovalRequest>(x => x.InvoiceId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(x => new { x.AssignedRole, x.CurrentDecision });
        });

        modelBuilder.Entity<ApprovalAction>(entity =>
        {
            entity.Property(x => x.Actor).HasMaxLength(128);
            entity.Property(x => x.Comment).HasMaxLength(500);
            entity.Property(x => x.Decision).HasConversion<string>().HasMaxLength(32);
        });

        modelBuilder.Entity<AccountingExport>(entity =>
        {
            entity.Property(x => x.ExportReference).HasMaxLength(64);
            entity.Property(x => x.Format).HasMaxLength(16);
            entity.Property(x => x.Status).HasMaxLength(32);
            entity.Property(x => x.GeneratedBy).HasMaxLength(128);
            entity.Property(x => x.TotalAmount).HasPrecision(18, 2);
            entity.Property(x => x.Payload).HasMaxLength(1000000);
            entity.Property(x => x.RowVersion).IsConcurrencyToken();
            entity.HasIndex(x => x.ExportReference).IsUnique();
        });

        modelBuilder.Entity<AccountingExportInvoice>(entity =>
        {
            entity.HasOne(x => x.AccountingExport)
                .WithMany(x => x.ExportInvoices)
                .HasForeignKey(x => x.AccountingExportId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(x => x.Invoice)
                .WithMany(x => x.AccountingExportInvoices)
                .HasForeignKey(x => x.InvoiceId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(x => new { x.AccountingExportId, x.InvoiceId }).IsUnique();
        });

        modelBuilder.Entity<Payment>(entity =>
        {
            entity.Property(x => x.Amount).HasPrecision(18, 2);
            entity.Property(x => x.Method).HasMaxLength(32);
            entity.Property(x => x.ReferenceNumber).HasMaxLength(64);
            entity.Property(x => x.RecordedBy).HasMaxLength(128);
            entity.Property(x => x.RowVersion).IsConcurrencyToken();
            entity.HasOne(x => x.Invoice)
                .WithMany(x => x.Payments)
                .HasForeignKey(x => x.InvoiceId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x => new { x.InvoiceId, x.PaymentDate });
        });

        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.Property(x => x.EntityName).HasMaxLength(80);
            entity.Property(x => x.EntityId).HasMaxLength(80);
            entity.Property(x => x.Action).HasMaxLength(80);
            entity.Property(x => x.Actor).HasMaxLength(128);
            entity.Property(x => x.Details).HasMaxLength(1000);
            entity.Property(x => x.IpAddress).HasMaxLength(45);
            entity.Property(x => x.UserAgent).HasMaxLength(512);
            entity.Property(x => x.CorrelationId).HasMaxLength(128);
            entity.HasIndex(x => x.OccurredAtUtc);
            entity.HasIndex(x => new { x.EntityName, x.Action });
            entity.HasIndex(x => x.CorrelationId);
        });

        modelBuilder.Entity<SupplierMappingProfile>(entity =>
        {
            entity.Property(x => x.DefaultTaxRate).HasPrecision(9, 4);
            entity.Property(x => x.UpdatedBy).HasMaxLength(128);
            entity.HasOne(x => x.Supplier)
                .WithOne(x => x.MappingProfile)
                .HasForeignKey<SupplierMappingProfile>(x => x.SupplierId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(x => x.SupplierId).IsUnique();
        });

        modelBuilder.Entity<SupplierItemMapping>(entity =>
        {
            entity.Property(x => x.ExternalItemCode).HasMaxLength(128);
            entity.Property(x => x.InternalItemCode).HasMaxLength(128);
            entity.Property(x => x.OverrideDescription).HasMaxLength(300);
            entity.Property(x => x.OverrideTaxRate).HasPrecision(9, 4);
            entity.Property(x => x.UpdatedBy).HasMaxLength(128);
            entity.HasOne(x => x.SupplierMappingProfile)
                .WithMany(x => x.ItemMappings)
                .HasForeignKey(x => x.SupplierMappingProfileId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(x => new { x.SupplierMappingProfileId, x.ExternalItemCode }).IsUnique();
        });

        modelBuilder.Entity<UserNotification>(entity =>
        {
            entity.Property(x => x.RecipientUsername).HasMaxLength(128);
            entity.Property(x => x.Category).HasMaxLength(64);
            entity.Property(x => x.Severity).HasMaxLength(16);
            entity.Property(x => x.Title).HasMaxLength(200);
            entity.Property(x => x.Message).HasMaxLength(1000);
            entity.Property(x => x.LinkUrl).HasMaxLength(260);
            entity.Property(x => x.SourceEntityName).HasMaxLength(80);
            entity.Property(x => x.SourceEntityId).HasMaxLength(80);
            entity.HasIndex(x => new { x.RecipientUsername, x.IsRead, x.CreatedAtUtc });
            entity.HasIndex(x => x.CreatedAtUtc);
        });

        modelBuilder.Entity<NotificationOutboxMessage>(entity =>
        {
            entity.Property(x => x.RecipientsJson).HasMaxLength(8000);
            entity.Property(x => x.Subject).HasMaxLength(200);
            entity.Property(x => x.Body).HasMaxLength(4000);
            entity.Property(x => x.LastError).HasMaxLength(1000);
            entity.HasIndex(x => new { x.DispatchedAtUtc, x.NextAttemptAtUtc });
            entity.HasIndex(x => x.CreatedAtUtc);
        });

        base.OnModelCreating(modelBuilder);
    }
}
