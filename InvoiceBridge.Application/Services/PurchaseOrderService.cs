using InvoiceBridge.Application.Abstractions.Persistence;
using InvoiceBridge.Application.Abstractions.Services;
using InvoiceBridge.Application.Common;
using InvoiceBridge.Application.DTOs;
using InvoiceBridge.Domain.Entities;
using InvoiceBridge.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace InvoiceBridge.Application.Services;

internal sealed class PurchaseOrderService(IApplicationDbContext dbContext) : IPurchaseOrderService
{
    public async Task<IReadOnlyList<SupplierLookupDto>> ListSuppliersAsync(CancellationToken cancellationToken = default)
    {
        return await dbContext.Suppliers
            .Where(s => s.IsActive)
            .OrderBy(s => s.LegalName)
            .Select(s => new SupplierLookupDto(s.Id, s.SupplierCode, s.LegalName))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<PurchaseOrderSummaryDto>> ListAsync(CancellationToken cancellationToken = default)
    {
        var purchaseOrders = await dbContext.PurchaseOrders
            .AsNoTracking()
            .Include(p => p.Supplier)
            .Include(p => p.Lines)
            .ToListAsync(cancellationToken);

        return purchaseOrders
            .Select(p => new PurchaseOrderSummaryDto(
                p.Id,
                p.PoNumber,
                p.Supplier.LegalName,
                p.Status,
                p.Lines.Sum(l => l.OrderedQuantity * l.UnitPrice * (1m + (l.TaxRate / 100m))),
                p.CreatedAtUtc))
            .OrderByDescending(p => p.CreatedAtUtc)
            .ToList();
    }

    public async Task<PurchaseOrderDetailsDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var po = await dbContext.PurchaseOrders
            .AsNoTracking()
            .Include(p => p.Supplier)
            .Include(p => p.Lines)
            .SingleOrDefaultAsync(p => p.Id == id, cancellationToken);

        if (po is null)
        {
            return null;
        }

        return new PurchaseOrderDetailsDto(
            po.Id,
            po.PoNumber,
            po.SupplierId,
            po.Supplier.LegalName,
            po.CurrencyCode,
            po.Status,
            po.ExpectedDeliveryDate,
            po.Lines
                .OrderBy(l => l.Id)
                .Select(l => new PurchaseOrderLineDto(
                    l.Id,
                    l.ItemCode,
                    l.Description,
                    l.OrderedQuantity,
                    l.UnitPrice,
                    l.TaxRate))
                .ToList());
    }

    public async Task<int> CreateAsync(CreatePurchaseOrderRequest request, CancellationToken cancellationToken = default)
    {
        if (request.SupplierId <= 0)
        {
            throw new ArgumentException("Supplier is required.", nameof(request.SupplierId));
        }

        var lines = request.Lines.Where(l => l.OrderedQuantity > 0 && !string.IsNullOrWhiteSpace(l.ItemCode)).ToList();
        if (lines.Count == 0)
        {
            throw new ArgumentException("At least one valid line item is required.", nameof(request.Lines));
        }

        var po = new PurchaseOrder
        {
            PoNumber = string.IsNullOrWhiteSpace(request.PoNumber)
                ? $"PO-{DateTime.UtcNow:yyyyMMddHHmmss}"
                : request.PoNumber.Trim(),
            SupplierId = request.SupplierId,
            CurrencyCode = request.CurrencyCode.Trim().ToUpperInvariant(),
            ExpectedDeliveryDate = request.ExpectedDeliveryDate,
            CreatedBy = request.CreatedBy.Trim(),
            Status = PurchaseOrderStatus.Submitted,
            Lines = lines.Select(line => new PurchaseOrderLine
            {
                ItemCode = line.ItemCode.Trim().ToUpperInvariant(),
                Description = line.Description.Trim(),
                OrderedQuantity = line.OrderedQuantity,
                UnitPrice = line.UnitPrice,
                TaxRate = line.TaxRate
            }).ToList()
        };

        dbContext.PurchaseOrders.Add(po);
        AuditTrailWriter.Add(
            dbContext,
            entityName: "PurchaseOrder",
            entityId: po.PoNumber,
            action: "PurchaseOrderCreated",
            actor: request.CreatedBy,
            details: $"SupplierId={po.SupplierId}; LineCount={po.Lines.Count}.");
        await dbContext.SaveChangesAsync(cancellationToken);

        return po.Id;
    }
}
