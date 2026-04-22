using InvoiceBridge.Application.Abstractions.Persistence;
using InvoiceBridge.Application.Abstractions.Services;
using InvoiceBridge.Application.Common;
using InvoiceBridge.Application.DTOs;
using InvoiceBridge.Application.Workflow;
using InvoiceBridge.Domain.Entities;
using InvoiceBridge.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace InvoiceBridge.Application.Services;

internal sealed class PaymentService(IApplicationDbContext dbContext) : IPaymentService
{
    public async Task<IReadOnlyList<PayableInvoiceDto>> ListPayablesAsync(CancellationToken cancellationToken = default)
    {
        var invoices = await dbContext.Invoices
            .AsNoTracking()
            .Include(invoice => invoice.Supplier)
            .Include(invoice => invoice.Payments)
            .Where(invoice => invoice.Status == InvoiceStatus.ReadyToPay || invoice.Status == InvoiceStatus.Paid)
            .OrderBy(invoice => invoice.DueDate)
            .ToListAsync(cancellationToken);

        return invoices
            .Select(invoice =>
            {
                var paidAmount = invoice.Payments.Sum(payment => payment.Amount);
                var outstandingAmount = PaymentBalanceCalculator.Outstanding(invoice.TotalAmount, paidAmount);

                return new PayableInvoiceDto(
                    invoice.Id,
                    invoice.InvoiceNumber,
                    invoice.Supplier.LegalName,
                    invoice.InvoiceDate,
                    invoice.DueDate,
                    invoice.CurrencyCode,
                    invoice.TotalAmount,
                    paidAmount,
                    outstandingAmount,
                    outstandingAmount == 0 ? InvoiceStatus.Paid : InvoiceStatus.ReadyToPay);
            })
            .Where(dto => dto.OutstandingAmount > 0)
            .ToList();
    }

    public async Task<IReadOnlyList<PaymentLedgerItemDto>> ListPaymentsAsync(CancellationToken cancellationToken = default)
    {
        var items = await dbContext.Payments
            .AsNoTracking()
            .Include(payment => payment.Invoice)
            .ThenInclude(invoice => invoice.Supplier)
            .Select(payment => new PaymentLedgerItemDto(
                payment.Id,
                payment.InvoiceId,
                payment.Invoice.InvoiceNumber,
                payment.Invoice.Supplier.LegalName,
                payment.Amount,
                payment.PaymentDate,
                payment.Method,
                payment.ReferenceNumber,
                payment.RecordedBy,
                payment.RecordedAtUtc))
            .ToListAsync(cancellationToken);

        return items
            .OrderByDescending(payment => payment.RecordedAtUtc)
            .ToList();
    }

    public async Task<int> RecordPaymentAsync(RecordPaymentRequest request, CancellationToken cancellationToken = default)
    {
        if (request.Amount <= 0)
        {
            throw new ArgumentException("Payment amount must be greater than zero.", nameof(request.Amount));
        }

        var invoice = await dbContext.Invoices
            .Include(i => i.Payments)
            .SingleOrDefaultAsync(i => i.Id == request.InvoiceId, cancellationToken);

        if (invoice is null)
        {
            throw new InvalidOperationException("Invoice not found.");
        }

        if (invoice.Status != InvoiceStatus.ReadyToPay && invoice.Status != InvoiceStatus.Paid)
        {
            throw new InvalidOperationException("Invoice is not ready for payment.");
        }

        var paidBefore = invoice.Payments.Sum(payment => payment.Amount);
        if (PaymentBalanceCalculator.IsPaid(invoice.TotalAmount, paidBefore))
        {
            throw new InvalidOperationException("Invoice is already fully paid.");
        }

        var payment = new Payment
        {
            InvoiceId = invoice.Id,
            Amount = request.Amount,
            PaymentDate = request.PaymentDate,
            Method = string.IsNullOrWhiteSpace(request.Method) ? "BankTransfer" : request.Method.Trim(),
            ReferenceNumber = string.IsNullOrWhiteSpace(request.ReferenceNumber)
                ? $"PAY-{DateTime.UtcNow:yyyyMMddHHmmssfff}"
                : request.ReferenceNumber.Trim(),
            RecordedBy = request.RecordedBy.Trim()
        };

        dbContext.Payments.Add(payment);

        var totalPaid = paidBefore + request.Amount;
        invoice.Status = PaymentBalanceCalculator.IsPaid(invoice.TotalAmount, totalPaid)
            ? InvoiceStatus.Paid
            : InvoiceStatus.ReadyToPay;

        AuditTrailWriter.Add(
            dbContext,
            entityName: "Invoice",
            entityId: invoice.Id.ToString(),
            action: invoice.Status == InvoiceStatus.Paid ? "PaymentRecordedFull" : "PaymentRecordedPartial",
            actor: request.RecordedBy,
            details: $"Payment {payment.ReferenceNumber} amount {request.Amount:0.00} via {payment.Method}.");

        await dbContext.SaveChangesAsync(cancellationToken);

        return payment.Id;
    }
}
