using InvoiceBridge.Application.Abstractions.Persistence;
using InvoiceBridge.Application.Abstractions.Services;
using InvoiceBridge.Application.Common;
using InvoiceBridge.Application.DTOs;
using InvoiceBridge.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace InvoiceBridge.Application.Services;

internal sealed class MappingProfileService(IApplicationDbContext dbContext) : IMappingProfileService
{
    public async Task<IReadOnlyList<SupplierMappingProfileDto>> ListProfilesAsync(CancellationToken cancellationToken = default)
    {
        var profiles = await dbContext.SupplierMappingProfiles
            .AsNoTracking()
            .Include(profile => profile.Supplier)
            .Include(profile => profile.ItemMappings)
            .ToListAsync(cancellationToken);

        return profiles
            .Select(profile => new SupplierMappingProfileDto(
                profile.Id,
                profile.SupplierId,
                profile.Supplier.SupplierCode,
                profile.Supplier.LegalName,
                profile.IsActive,
                profile.RequireMappedItems,
                profile.DefaultTaxRate,
                profile.UpdatedBy,
                profile.UpdatedAtUtc,
                profile.ItemMappings.Count(mapping => mapping.IsActive)))
            .OrderBy(item => item.SupplierName)
            .ToList();
    }

    public async Task<SupplierMappingProfileDto?> GetProfileBySupplierIdAsync(int supplierId, CancellationToken cancellationToken = default)
    {
        var profile = await dbContext.SupplierMappingProfiles
            .AsNoTracking()
            .Include(item => item.Supplier)
            .Include(item => item.ItemMappings)
            .SingleOrDefaultAsync(item => item.SupplierId == supplierId, cancellationToken);

        return profile is null
            ? null
            : new SupplierMappingProfileDto(
                profile.Id,
                profile.SupplierId,
                profile.Supplier.SupplierCode,
                profile.Supplier.LegalName,
                profile.IsActive,
                profile.RequireMappedItems,
                profile.DefaultTaxRate,
                profile.UpdatedBy,
                profile.UpdatedAtUtc,
                profile.ItemMappings.Count(mapping => mapping.IsActive));
    }

    public async Task<int> UpsertProfileAsync(UpsertSupplierMappingProfileRequest request, CancellationToken cancellationToken = default)
    {
        if (request.SupplierId <= 0)
        {
            throw new ArgumentException("Supplier is required.", nameof(request.SupplierId));
        }

        var supplierExists = await dbContext.Suppliers.AnyAsync(supplier => supplier.Id == request.SupplierId, cancellationToken);
        if (!supplierExists)
        {
            throw new InvalidOperationException("Supplier not found.");
        }

        SupplierMappingProfile profile;
        if (request.ProfileId.HasValue)
        {
            profile = await dbContext.SupplierMappingProfiles
                .SingleOrDefaultAsync(item => item.Id == request.ProfileId.Value, cancellationToken)
                ?? throw new InvalidOperationException("Mapping profile not found.");

            if (profile.SupplierId != request.SupplierId)
            {
                throw new InvalidOperationException("Supplier for mapping profile cannot be changed.");
            }

            profile.IsActive = request.IsActive;
            profile.RequireMappedItems = request.RequireMappedItems;
            profile.DefaultTaxRate = request.DefaultTaxRate;
            profile.UpdatedBy = request.UpdatedBy.Trim();
            profile.UpdatedAtUtc = DateTimeOffset.UtcNow;
        }
        else
        {
            var existing = await dbContext.SupplierMappingProfiles
                .SingleOrDefaultAsync(item => item.SupplierId == request.SupplierId, cancellationToken);

            if (existing is not null)
            {
                existing.IsActive = request.IsActive;
                existing.RequireMappedItems = request.RequireMappedItems;
                existing.DefaultTaxRate = request.DefaultTaxRate;
                existing.UpdatedBy = request.UpdatedBy.Trim();
                existing.UpdatedAtUtc = DateTimeOffset.UtcNow;
                profile = existing;
            }
            else
            {
                profile = new SupplierMappingProfile
                {
                    SupplierId = request.SupplierId,
                    IsActive = request.IsActive,
                    RequireMappedItems = request.RequireMappedItems,
                    DefaultTaxRate = request.DefaultTaxRate,
                    UpdatedBy = request.UpdatedBy.Trim()
                };

                dbContext.SupplierMappingProfiles.Add(profile);
            }
        }

        AuditTrailWriter.Add(
            dbContext,
            entityName: "SupplierMappingProfile",
            entityId: profile.SupplierId.ToString(),
            action: "ProfileUpserted",
            actor: request.UpdatedBy,
            details: $"IsActive={profile.IsActive}; RequireMappedItems={profile.RequireMappedItems}; DefaultTaxRate={profile.DefaultTaxRate?.ToString("0.####") ?? "null"}.");

        await dbContext.SaveChangesAsync(cancellationToken);
        return profile.Id;
    }

    public async Task<IReadOnlyList<SupplierItemMappingDto>> ListItemMappingsAsync(int profileId, CancellationToken cancellationToken = default)
    {
        var mappings = await dbContext.SupplierItemMappings
            .AsNoTracking()
            .Where(mapping => mapping.SupplierMappingProfileId == profileId)
            .ToListAsync(cancellationToken);

        return mappings
            .Select(mapping => new SupplierItemMappingDto(
                mapping.Id,
                mapping.SupplierMappingProfileId,
                mapping.ExternalItemCode,
                mapping.InternalItemCode,
                mapping.OverrideDescription,
                mapping.OverrideTaxRate,
                mapping.IsActive,
                mapping.UpdatedBy,
                mapping.UpdatedAtUtc))
            .OrderBy(mapping => mapping.ExternalItemCode)
            .ToList();
    }

    public async Task<int> UpsertItemMappingAsync(UpsertSupplierItemMappingRequest request, CancellationToken cancellationToken = default)
    {
        if (request.ProfileId <= 0)
        {
            throw new ArgumentException("Profile is required.", nameof(request.ProfileId));
        }

        if (string.IsNullOrWhiteSpace(request.ExternalItemCode) || string.IsNullOrWhiteSpace(request.InternalItemCode))
        {
            throw new ArgumentException("External and internal item codes are required.");
        }

        var profile = await dbContext.SupplierMappingProfiles
            .SingleOrDefaultAsync(item => item.Id == request.ProfileId, cancellationToken)
            ?? throw new InvalidOperationException("Mapping profile not found.");

        SupplierItemMapping mapping;
        if (request.MappingId.HasValue)
        {
            mapping = await dbContext.SupplierItemMappings
                .SingleOrDefaultAsync(item => item.Id == request.MappingId.Value, cancellationToken)
                ?? throw new InvalidOperationException("Mapping entry not found.");

            if (mapping.SupplierMappingProfileId != request.ProfileId)
            {
                throw new InvalidOperationException("Mapping entry does not belong to selected profile.");
            }
        }
        else
        {
            mapping = await dbContext.SupplierItemMappings.SingleOrDefaultAsync(
                item => item.SupplierMappingProfileId == request.ProfileId
                    && item.ExternalItemCode == request.ExternalItemCode.Trim().ToUpperInvariant(),
                cancellationToken)
                ?? new SupplierItemMapping
                {
                    SupplierMappingProfileId = request.ProfileId,
                    ExternalItemCode = request.ExternalItemCode.Trim().ToUpperInvariant(),
                    InternalItemCode = request.InternalItemCode.Trim().ToUpperInvariant(),
                    UpdatedBy = request.UpdatedBy.Trim()
                };

            if (mapping.Id == 0)
            {
                dbContext.SupplierItemMappings.Add(mapping);
            }
        }

        mapping.ExternalItemCode = request.ExternalItemCode.Trim().ToUpperInvariant();
        mapping.InternalItemCode = request.InternalItemCode.Trim().ToUpperInvariant();
        mapping.OverrideDescription = string.IsNullOrWhiteSpace(request.OverrideDescription)
            ? null
            : request.OverrideDescription.Trim();
        mapping.OverrideTaxRate = request.OverrideTaxRate;
        mapping.IsActive = request.IsActive;
        mapping.UpdatedBy = request.UpdatedBy.Trim();
        mapping.UpdatedAtUtc = DateTimeOffset.UtcNow;

        profile.UpdatedAtUtc = DateTimeOffset.UtcNow;
        profile.UpdatedBy = request.UpdatedBy.Trim();

        AuditTrailWriter.Add(
            dbContext,
            entityName: "SupplierItemMapping",
            entityId: $"{request.ProfileId}:{mapping.ExternalItemCode}",
            action: "ItemMappingUpserted",
            actor: request.UpdatedBy,
            details: $"Internal={mapping.InternalItemCode}; IsActive={mapping.IsActive}.");

        await dbContext.SaveChangesAsync(cancellationToken);
        return mapping.Id;
    }

    public async Task DeleteItemMappingAsync(int mappingId, string actor, CancellationToken cancellationToken = default)
    {
        var mapping = await dbContext.SupplierItemMappings
            .SingleOrDefaultAsync(item => item.Id == mappingId, cancellationToken)
            ?? throw new InvalidOperationException("Mapping entry not found.");

        dbContext.SupplierItemMappings.Remove(mapping);

        AuditTrailWriter.Add(
            dbContext,
            entityName: "SupplierItemMapping",
            entityId: mappingId.ToString(),
            action: "ItemMappingDeleted",
            actor: actor,
            details: $"External={mapping.ExternalItemCode}; Internal={mapping.InternalItemCode}.");

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
