using System.Globalization;
using System.Text;
using System.Xml.Linq;
using FluentValidation;
using InvoiceBridge.Application.Abstractions.Persistence;
using InvoiceBridge.Application.Abstractions.Services;
using InvoiceBridge.Application.Common;
using InvoiceBridge.Application.DTOs;
using InvoiceBridge.Domain.Entities;
using InvoiceBridge.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace InvoiceBridge.Application.Services;

internal sealed class AccountingExportService(
    IApplicationDbContext dbContext,
    IValidator<CreateAccountingExportRequest> createExportValidator) : IAccountingExportService
{
    public async Task<IReadOnlyList<ExportCandidateInvoiceDto>> ListEligibleInvoicesAsync(CancellationToken cancellationToken = default)
    {
        return await dbContext.Invoices
            .AsNoTracking()
            .Include(invoice => invoice.Supplier)
            .Where(invoice => invoice.Status == InvoiceStatus.Approved)
            .OrderBy(invoice => invoice.InvoiceDate)
            .Select(invoice => new ExportCandidateInvoiceDto(
                invoice.Id,
                invoice.InvoiceNumber,
                invoice.Supplier.SupplierCode,
                invoice.Supplier.LegalName,
                invoice.InvoiceDate,
                invoice.DueDate,
                invoice.CurrencyCode,
                invoice.Subtotal,
                invoice.TaxAmount,
                invoice.TotalAmount))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AccountingExportSummaryDto>> ListExportsAsync(CancellationToken cancellationToken = default)
    {
        var exports = await dbContext.AccountingExports
            .AsNoTracking()
            .Select(export => new AccountingExportSummaryDto(
                export.Id,
                export.ExportReference,
                export.Format,
                export.Status,
                export.InvoiceCount,
                export.TotalAmount,
                export.GeneratedBy,
                export.GeneratedAtUtc))
            .ToListAsync(cancellationToken);

        return exports
            .OrderByDescending(export => export.GeneratedAtUtc)
            .ToList();
    }

    public async Task<AccountingExportResultDto> CreateExportAsync(CreateAccountingExportRequest request, CancellationToken cancellationToken = default)
    {
        await createExportValidator.ValidateAndThrowAsync(request, cancellationToken);

        var eligibleInvoicesQuery = dbContext.Invoices
            .Include(invoice => invoice.Supplier)
            .Where(invoice => invoice.Status == InvoiceStatus.Approved);

        if (!request.IncludeAllEligible && request.InvoiceIds.Count > 0)
        {
            eligibleInvoicesQuery = eligibleInvoicesQuery.Where(invoice => request.InvoiceIds.Contains(invoice.Id));
        }

        var invoices = await eligibleInvoicesQuery
            .OrderBy(invoice => invoice.InvoiceDate)
            .ToListAsync(cancellationToken);

        if (invoices.Count == 0)
        {
            throw new InvalidOperationException("No approved invoices available for export.");
        }

        var exportFormat = string.IsNullOrWhiteSpace(request.Format)
            ? "CSV"
            : request.Format.Trim().ToUpperInvariant();

        var payload = exportFormat == "XML"
            ? BuildXmlPayload(invoices)
            : BuildCsvPayload(invoices);

        var export = new AccountingExport
        {
            ExportReference = $"EXP-{DateTime.UtcNow:yyyyMMddHHmmssfff}",
            Format = exportFormat,
            Status = "Generated",
            GeneratedBy = request.GeneratedBy.Trim(),
            InvoiceCount = invoices.Count,
            TotalAmount = invoices.Sum(invoice => invoice.TotalAmount),
            Payload = payload,
            ExportInvoices = invoices.Select(invoice => new AccountingExportInvoice
            {
                InvoiceId = invoice.Id
            }).ToList()
        };

        dbContext.AccountingExports.Add(export);

        foreach (var invoice in invoices)
        {
            invoice.Status = InvoiceStatus.ReadyToPay;

            AuditTrailWriter.Add(
                dbContext,
                entityName: "Invoice",
                entityId: invoice.Id.ToString(),
                action: "InvoiceExported",
                actor: request.GeneratedBy,
                details: $"Included in export {export.ExportReference} ({exportFormat}).");
        }

        AuditTrailWriter.Add(
            dbContext,
            entityName: "AccountingExport",
            entityId: export.ExportReference,
            action: "ExportGenerated",
            actor: request.GeneratedBy,
            details: $"{export.InvoiceCount} invoices, total {export.TotalAmount.ToString("0.00", CultureInfo.InvariantCulture)}.");

        await dbContext.SaveChangesAsync(cancellationToken);

        return new AccountingExportResultDto
        {
            ExportId = export.Id,
            ExportReference = export.ExportReference,
            Format = export.Format,
            InvoiceCount = export.InvoiceCount,
            TotalAmount = export.TotalAmount,
            GeneratedAtUtc = export.GeneratedAtUtc,
            Payload = export.Payload
        };
    }

    public async Task<string?> GetPayloadAsync(int exportId, CancellationToken cancellationToken = default)
    {
        return await dbContext.AccountingExports
            .AsNoTracking()
            .Where(export => export.Id == exportId)
            .Select(export => export.Payload)
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<AccountingExportDownloadDto?> GetDownloadAsync(int exportId, CancellationToken cancellationToken = default)
    {
        var export = await dbContext.AccountingExports
            .AsNoTracking()
            .Where(item => item.Id == exportId)
            .Select(item => new
            {
                item.ExportReference,
                item.Format,
                item.Payload
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (export is null)
        {
            return null;
        }

        var normalizedFormat = string.IsNullOrWhiteSpace(export.Format)
            ? "csv"
            : export.Format.Trim().ToLowerInvariant();

        var extension = normalizedFormat switch
        {
            "xml" => "xml",
            _ => "csv"
        };

        var contentType = extension switch
        {
            "xml" => "application/xml; charset=utf-8",
            _ => "text/csv; charset=utf-8"
        };

        return new AccountingExportDownloadDto
        {
            FileName = $"{export.ExportReference}.{extension}",
            ContentType = contentType,
            Content = export.Payload ?? string.Empty
        };
    }

    private static string BuildCsvPayload(IReadOnlyList<Invoice> invoices)
    {
        var builder = new StringBuilder();
        builder.AppendLine("InvoiceNumber,SupplierCode,SupplierName,InvoiceDate,DueDate,CurrencyCode,Subtotal,TaxAmount,TotalAmount");

        foreach (var invoice in invoices)
        {
            builder.Append(EscapeCsv(invoice.InvoiceNumber)).Append(',')
                .Append(EscapeCsv(invoice.Supplier.SupplierCode)).Append(',')
                .Append(EscapeCsv(invoice.Supplier.LegalName)).Append(',')
                .Append(invoice.InvoiceDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)).Append(',')
                .Append(invoice.DueDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? string.Empty).Append(',')
                .Append(EscapeCsv(invoice.CurrencyCode)).Append(',')
                .Append(invoice.Subtotal.ToString("0.00", CultureInfo.InvariantCulture)).Append(',')
                .Append(invoice.TaxAmount.ToString("0.00", CultureInfo.InvariantCulture)).Append(',')
                .Append(invoice.TotalAmount.ToString("0.00", CultureInfo.InvariantCulture))
                .AppendLine();
        }

        return builder.ToString();
    }

    private static string BuildXmlPayload(IReadOnlyList<Invoice> invoices)
    {
        var root = new XElement("AccountingExport",
            new XAttribute("generatedAtUtc", DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture)),
            invoices.Select(invoice => new XElement("Invoice",
                new XElement("InvoiceNumber", invoice.InvoiceNumber),
                new XElement("SupplierCode", invoice.Supplier.SupplierCode),
                new XElement("SupplierName", invoice.Supplier.LegalName),
                new XElement("InvoiceDate", invoice.InvoiceDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)),
                new XElement("DueDate", invoice.DueDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? string.Empty),
                new XElement("CurrencyCode", invoice.CurrencyCode),
                new XElement("Subtotal", invoice.Subtotal.ToString("0.00", CultureInfo.InvariantCulture)),
                new XElement("TaxAmount", invoice.TaxAmount.ToString("0.00", CultureInfo.InvariantCulture)),
                new XElement("TotalAmount", invoice.TotalAmount.ToString("0.00", CultureInfo.InvariantCulture)))));

        return root.ToString(SaveOptions.DisableFormatting);
    }

    private static string EscapeCsv(string value)
    {
        if (!value.Contains(',') && !value.Contains('"') && !value.Contains('\n'))
        {
            return value;
        }

        return $"\"{value.Replace("\"", "\"\"")}\"";
    }
}
