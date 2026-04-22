using InvoiceBridge.Application.Abstractions.Persistence;
using InvoiceBridge.Application.Abstractions.Services;
using InvoiceBridge.Application.Common;
using InvoiceBridge.Application.DTOs;
using InvoiceBridge.Domain.Entities;
using InvoiceBridge.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace InvoiceBridge.Application.Services;

internal sealed class GoodsReceiptService(IApplicationDbContext dbContext) : IGoodsReceiptService
{
    public async Task<IReadOnlyList<OpenPurchaseOrderDto>> ListOpenPurchaseOrdersAsync(CancellationToken cancellationToken = default)
    {
        var items = await dbContext.PurchaseOrders
            .AsNoTracking()
            .Include(p => p.Supplier)
            .Include(p => p.Lines)
            .Where(p => p.Status == PurchaseOrderStatus.Submitted || p.Status == PurchaseOrderStatus.PartiallyReceived)
            .Select(p => new OpenPurchaseOrderDto(
                p.Id,
                p.PoNumber,
                p.Supplier.LegalName,
                p.CurrencyCode,
                p.Lines.Select(line => new PurchaseOrderLineDto(
                    line.Id,
                    line.ItemCode,
                    line.Description,
                    line.OrderedQuantity,
                    line.UnitPrice,
                    line.TaxRate)).ToList()))
            .ToListAsync(cancellationToken);

        return items
            .OrderByDescending(item => item.Id)
            .ToList();
    }

    public async Task<IReadOnlyList<GoodsReceiptSummaryDto>> ListAsync(CancellationToken cancellationToken = default)
    {
        var receipts = await dbContext.GoodsReceipts
            .AsNoTracking()
            .Include(g => g.PurchaseOrder)
            .Include(g => g.Lines)
            .ToListAsync(cancellationToken);

        return receipts
            .Select(g => new GoodsReceiptSummaryDto(
                g.Id,
                g.GrnNumber,
                g.PurchaseOrder.PoNumber,
                g.ReceivedBy,
                g.ReceivedAtUtc,
                g.Lines.Sum(l => l.ReceivedQuantity)))
            .OrderByDescending(item => item.ReceivedAtUtc)
            .ToList();
    }

    public async Task<int> CreateAsync(CreateGoodsReceiptRequest request, CancellationToken cancellationToken = default)
    {
        var po = await dbContext.PurchaseOrders
            .Include(p => p.Lines)
            .ThenInclude(l => l.GoodsReceiptLines)
            .SingleOrDefaultAsync(p => p.Id == request.PurchaseOrderId, cancellationToken);

        if (po is null)
        {
            throw new InvalidOperationException("Purchase order not found.");
        }

        var lineInputs = request.Lines.Where(l => l.ReceivedQuantity > 0 || l.DamagedQuantity > 0).ToList();
        if (lineInputs.Count == 0)
        {
            throw new ArgumentException("At least one line must have received or damaged quantity.", nameof(request.Lines));
        }

        var validPoLineIds = po.Lines.Select(l => l.Id).ToHashSet();
        foreach (var line in lineInputs)
        {
            if (!validPoLineIds.Contains(line.PurchaseOrderLineId))
            {
                throw new InvalidOperationException("One or more receipt lines do not belong to the selected PO.");
            }
        }

        var receipt = new GoodsReceipt
        {
            GrnNumber = string.IsNullOrWhiteSpace(request.GrnNumber)
                ? $"GRN-{DateTime.UtcNow:yyyyMMddHHmmss}"
                : request.GrnNumber.Trim(),
            PurchaseOrderId = po.Id,
            ReceivedBy = request.ReceivedBy.Trim(),
            Lines = lineInputs.Select(line => new GoodsReceiptLine
            {
                PurchaseOrderLineId = line.PurchaseOrderLineId,
                ReceivedQuantity = line.ReceivedQuantity,
                DamagedQuantity = line.DamagedQuantity
            }).ToList()
        };

        dbContext.GoodsReceipts.Add(receipt);

        var isFullyReceived = po.Lines.All(line =>
        {
            var received = line.GoodsReceiptLines.Sum(grnLine => grnLine.ReceivedQuantity)
                + lineInputs.Where(i => i.PurchaseOrderLineId == line.Id).Sum(i => i.ReceivedQuantity);

            return received >= line.OrderedQuantity;
        });

        po.Status = isFullyReceived ? PurchaseOrderStatus.FullyReceived : PurchaseOrderStatus.PartiallyReceived;

        AuditTrailWriter.Add(
            dbContext,
            entityName: "GoodsReceipt",
            entityId: receipt.GrnNumber,
            action: "GoodsReceiptCreated",
            actor: request.ReceivedBy,
            details: $"PO={po.PoNumber}; Lines={receipt.Lines.Count}; Status={po.Status}.");

        await dbContext.SaveChangesAsync(cancellationToken);
        return receipt.Id;
    }
}
