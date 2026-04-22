using InvoiceBridge.Application.DTOs;

namespace InvoiceBridge.Application.Abstractions.Services;

public interface IAuditService
{
    Task<IReadOnlyList<AuditLogDto>> ListAsync(AuditQueryRequest request, CancellationToken cancellationToken = default);
}
