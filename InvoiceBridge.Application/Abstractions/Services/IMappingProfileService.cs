using InvoiceBridge.Application.DTOs;

namespace InvoiceBridge.Application.Abstractions.Services;

public interface IMappingProfileService
{
    Task<IReadOnlyList<SupplierMappingProfileDto>> ListProfilesAsync(CancellationToken cancellationToken = default);
    Task<SupplierMappingProfileDto?> GetProfileBySupplierIdAsync(int supplierId, CancellationToken cancellationToken = default);
    Task<int> UpsertProfileAsync(UpsertSupplierMappingProfileRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SupplierItemMappingDto>> ListItemMappingsAsync(int profileId, CancellationToken cancellationToken = default);
    Task<int> UpsertItemMappingAsync(UpsertSupplierItemMappingRequest request, CancellationToken cancellationToken = default);
    Task DeleteItemMappingAsync(int mappingId, string actor, CancellationToken cancellationToken = default);
}
