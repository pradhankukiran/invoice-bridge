using InvoiceBridge.Domain.Entities;
using InvoiceBridge.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InvoiceBridge.Infrastructure.Seed;

internal static class SeedData
{
    public static async Task EnsureSeededAsync(InvoiceBridgeDbContext dbContext, CancellationToken cancellationToken = default)
    {
        if (!await dbContext.Suppliers.AnyAsync(cancellationToken))
        {
            dbContext.Suppliers.AddRange(
                new Supplier
                {
                    SupplierCode = "GOV-SUP-001",
                    LegalName = "National Office Supplies Cooperative",
                    Email = "sales@nosc.example",
                    IsActive = true
                },
                new Supplier
                {
                    SupplierCode = "GOV-SUP-002",
                    LegalName = "Civic Utilities Equipment Ltd",
                    Email = "accounts@cue.example",
                    IsActive = true
                });
        }

        if (!await dbContext.Products.AnyAsync(cancellationToken))
        {
            dbContext.Products.AddRange(
                new Product
                {
                    Sku = "PAPER-A4-BOX",
                    Name = "A4 Paper Box",
                    DefaultUnitPrice = 34.95m,
                    IsActive = true
                },
                new Product
                {
                    Sku = "INK-BLACK-XL",
                    Name = "Printer Ink Black XL",
                    DefaultUnitPrice = 58.20m,
                    IsActive = true
                },
                new Product
                {
                    Sku = "CHAIR-ERG-01",
                    Name = "Ergonomic Chair",
                    DefaultUnitPrice = 239.99m,
                    IsActive = true
                });
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        if (!await dbContext.SupplierMappingProfiles.AnyAsync(cancellationToken))
        {
            var supplier = await dbContext.Suppliers
                .OrderBy(item => item.SupplierCode)
                .FirstOrDefaultAsync(item => item.SupplierCode == "GOV-SUP-001", cancellationToken);

            if (supplier is not null)
            {
                dbContext.SupplierMappingProfiles.Add(new SupplierMappingProfile
                {
                    SupplierId = supplier.Id,
                    IsActive = true,
                    RequireMappedItems = false,
                    DefaultTaxRate = 5m,
                    UpdatedBy = "seed.system",
                    ItemMappings =
                    [
                        new SupplierItemMapping
                        {
                            ExternalItemCode = "EXT-PAPER-A4",
                            InternalItemCode = "PAPER-A4-BOX",
                            OverrideDescription = "A4 Paper Box",
                            OverrideTaxRate = 5m,
                            IsActive = true,
                            UpdatedBy = "seed.system"
                        },
                        new SupplierItemMapping
                        {
                            ExternalItemCode = "EXT-INK-BLK-XL",
                            InternalItemCode = "INK-BLACK-XL",
                            OverrideDescription = "Printer Ink Black XL",
                            OverrideTaxRate = 5m,
                            IsActive = true,
                            UpdatedBy = "seed.system"
                        }
                    ]
                });

                await dbContext.SaveChangesAsync(cancellationToken);
            }
        }
    }
}
