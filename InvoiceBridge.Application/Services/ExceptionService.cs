using InvoiceBridge.Application.Abstractions.Persistence;
using InvoiceBridge.Application.Abstractions.Services;
using InvoiceBridge.Application.Common;
using InvoiceBridge.Application.DTOs;
using InvoiceBridge.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace InvoiceBridge.Application.Services;

internal sealed class ExceptionService(IApplicationDbContext dbContext, IInvoiceService invoiceService) : IExceptionService
{
    public async Task<IReadOnlyList<ExceptionInvoiceDto>> ListAsync(CancellationToken cancellationToken = default)
    {
        var invoices = await dbContext.Invoices
            .AsNoTracking()
            .Include(invoice => invoice.Supplier)
            .Include(invoice => invoice.MatchResults)
            .Where(invoice => invoice.Status == InvoiceStatus.Exception)
            .ToListAsync(cancellationToken);

        return invoices
            .Select(invoice =>
            {
                var latest = invoice.MatchResults
                    .OrderByDescending(result => result.ExecutedAtUtc)
                    .FirstOrDefault();

                return new ExceptionInvoiceDto(
                    invoice.Id,
                    invoice.InvoiceNumber,
                    invoice.Supplier.LegalName,
                    invoice.InvoiceDate,
                    invoice.TotalAmount,
                    latest?.ResultCode,
                    latest?.ExecutedAtUtc);
            })
            .OrderByDescending(invoice => invoice.InvoiceId)
            .ToList();
    }

    public async Task<ExceptionResolutionResultDto> ResolveAsync(ExceptionResolutionRequest request, CancellationToken cancellationToken = default)
    {
        var invoice = await dbContext.Invoices
            .SingleOrDefaultAsync(i => i.Id == request.InvoiceId, cancellationToken);

        if (invoice is null)
        {
            throw new InvalidOperationException("Invoice not found.");
        }

        if (invoice.Status != InvoiceStatus.Exception)
        {
            throw new InvalidOperationException("Invoice is not in exception state.");
        }

        AuditTrailWriter.Add(
            dbContext,
            entityName: "Invoice",
            entityId: invoice.Id.ToString(),
            action: request.RerunMatch ? "ExceptionResolvedRerunMatch" : "ExceptionResolutionNote",
            actor: request.Actor,
            details: string.IsNullOrWhiteSpace(request.Note)
                ? "Exception reviewed."
                : request.Note);

        MatchRunResultDto? matchRunResult = null;
        var finalStatus = invoice.Status;
        if (request.RerunMatch)
        {
            matchRunResult = await invoiceService.RunMatchAsync(invoice.Id, request.Actor, cancellationToken);
            finalStatus = await dbContext.Invoices
                .Where(i => i.Id == invoice.Id)
                .Select(i => i.Status)
                .SingleAsync(cancellationToken);
        }
        else
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return new ExceptionResolutionResultDto
        {
            InvoiceId = invoice.Id,
            InvoiceStatus = finalStatus,
            Message = request.RerunMatch
                ? (matchRunResult?.IsMatch == true ? "Re-match succeeded. Invoice moved forward." : "Re-match still failed. Invoice remains in exception flow.")
                : "Exception note saved. No re-match executed.",
            MatchResult = matchRunResult
        };
    }
}
