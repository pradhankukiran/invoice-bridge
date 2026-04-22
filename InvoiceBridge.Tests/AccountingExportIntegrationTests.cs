using InvoiceBridge.Application.Abstractions.Services;
using InvoiceBridge.Application.DTOs;
using InvoiceBridge.Domain.Entities;
using InvoiceBridge.Domain.Enums;
using InvoiceBridge.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace InvoiceBridge.Tests;

public sealed class AccountingExportIntegrationTests
{
    [Fact]
    public async Task CreateExportAsync_CsvSelection_TransitionsSelectedInvoicesAndProvidesDownload()
    {
        await using var testScope = await IntegrationTestScope.CreateAsync();
        using var scope = testScope.CreateScope();

        var dbContext = scope.ServiceProvider.GetRequiredService<InvoiceBridgeDbContext>();
        var exportService = scope.ServiceProvider.GetRequiredService<IAccountingExportService>();

        var (firstInvoiceId, secondInvoiceId) = await CreateApprovedInvoicesAsync(dbContext);

        var result = await exportService.CreateExportAsync(new CreateAccountingExportRequest
        {
            GeneratedBy = "finance.officer",
            Format = "CSV",
            IncludeAllEligible = false,
            InvoiceIds = [firstInvoiceId]
        });

        Assert.Equal(1, result.InvoiceCount);
        Assert.Equal("CSV", result.Format);
        Assert.Contains("INV-EXP-001", result.Payload, StringComparison.Ordinal);
        Assert.DoesNotContain("INV-EXP-002", result.Payload, StringComparison.Ordinal);

        var firstInvoice = await dbContext.Invoices.SingleAsync(item => item.Id == firstInvoiceId);
        var secondInvoice = await dbContext.Invoices.SingleAsync(item => item.Id == secondInvoiceId);

        Assert.Equal(InvoiceStatus.ReadyToPay, firstInvoice.Status);
        Assert.Equal(InvoiceStatus.Approved, secondInvoice.Status);

        var download = await exportService.GetDownloadAsync(result.ExportId);
        Assert.NotNull(download);
        Assert.Equal("text/csv; charset=utf-8", download!.ContentType);
        Assert.EndsWith(".csv", download.FileName, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("INV-EXP-001", download.Content, StringComparison.Ordinal);
        Assert.DoesNotContain("INV-EXP-002", download.Content, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CreateExportAsync_XmlFormat_ProducesXmlArtifact()
    {
        await using var testScope = await IntegrationTestScope.CreateAsync();
        using var scope = testScope.CreateScope();

        var dbContext = scope.ServiceProvider.GetRequiredService<InvoiceBridgeDbContext>();
        var exportService = scope.ServiceProvider.GetRequiredService<IAccountingExportService>();

        await CreateApprovedInvoicesAsync(dbContext);

        var result = await exportService.CreateExportAsync(new CreateAccountingExportRequest
        {
            GeneratedBy = "finance.officer",
            Format = "XML",
            IncludeAllEligible = true
        });

        Assert.Equal("XML", result.Format);
        Assert.StartsWith("<AccountingExport", result.Payload, StringComparison.Ordinal);

        var artifact = await exportService.GetDownloadAsync(result.ExportId);
        Assert.NotNull(artifact);
        Assert.Equal("application/xml; charset=utf-8", artifact!.ContentType);
        Assert.EndsWith(".xml", artifact.FileName, StringComparison.OrdinalIgnoreCase);
        Assert.StartsWith("<AccountingExport", artifact.Content, StringComparison.Ordinal);
    }

    private static async Task<(int FirstInvoiceId, int SecondInvoiceId)> CreateApprovedInvoicesAsync(InvoiceBridgeDbContext dbContext)
    {
        var supplier = new Supplier
        {
            SupplierCode = "SUP-EXP-001",
            LegalName = "Export Supplier",
            Email = "export.supplier@example.test",
            IsActive = true
        };

        var first = new Invoice
        {
            InvoiceNumber = "INV-EXP-001",
            Supplier = supplier,
            CurrencyCode = "USD",
            InvoiceDate = new DateOnly(2026, 2, 10),
            DueDate = new DateOnly(2026, 2, 28),
            Subtotal = 100m,
            TaxAmount = 5m,
            TotalAmount = 105m,
            Status = InvoiceStatus.Approved,
            Lines =
            [
                new InvoiceLine
                {
                    LineNumber = 1,
                    ItemCode = "ITEM-EXP-001",
                    Description = "Export Item 1",
                    BilledQuantity = 1m,
                    UnitPrice = 100m,
                    TaxRate = 5m,
                    LineTotal = 100m
                }
            ]
        };

        var second = new Invoice
        {
            InvoiceNumber = "INV-EXP-002",
            Supplier = supplier,
            CurrencyCode = "USD",
            InvoiceDate = new DateOnly(2026, 2, 11),
            DueDate = new DateOnly(2026, 2, 28),
            Subtotal = 200m,
            TaxAmount = 10m,
            TotalAmount = 210m,
            Status = InvoiceStatus.Approved,
            Lines =
            [
                new InvoiceLine
                {
                    LineNumber = 1,
                    ItemCode = "ITEM-EXP-002",
                    Description = "Export Item 2",
                    BilledQuantity = 2m,
                    UnitPrice = 100m,
                    TaxRate = 5m,
                    LineTotal = 200m
                }
            ]
        };

        dbContext.Invoices.AddRange(first, second);
        await dbContext.SaveChangesAsync();

        return (first.Id, second.Id);
    }
}
