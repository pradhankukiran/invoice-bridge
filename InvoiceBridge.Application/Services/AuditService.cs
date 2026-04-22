using InvoiceBridge.Application.Abstractions.Persistence;
using InvoiceBridge.Application.Abstractions.Services;
using InvoiceBridge.Application.DTOs;
using Microsoft.EntityFrameworkCore;

namespace InvoiceBridge.Application.Services;

internal sealed class AuditService(IApplicationDbContext dbContext) : IAuditService
{
    public async Task<IReadOnlyList<AuditLogDto>> ListAsync(AuditQueryRequest request, CancellationToken cancellationToken = default)
    {
        var maxRows = request.MaxRows switch
        {
            < 1 => 250,
            > 1000 => 1000,
            _ => request.MaxRows
        };

        var query = dbContext.AuditLogs.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.EntityName))
        {
            query = query.Where(log => log.EntityName == request.EntityName.Trim());
        }

        if (!string.IsNullOrWhiteSpace(request.Action))
        {
            query = query.Where(log => log.Action == request.Action.Trim());
        }

        if (!string.IsNullOrWhiteSpace(request.Actor))
        {
            var actorFilter = request.Actor.Trim().ToLower();
            query = query.Where(log => log.Actor.ToLower().Contains(actorFilter));
        }

        if (request.FromUtc.HasValue)
        {
            query = query.Where(log => log.OccurredAtUtc >= request.FromUtc.Value);
        }

        if (request.ToUtc.HasValue)
        {
            query = query.Where(log => log.OccurredAtUtc <= request.ToUtc.Value);
        }

        var logs = await query
            .Select(log => new AuditLogDto(
                log.Id,
                log.EntityName,
                log.EntityId,
                log.Action,
                log.Actor,
                log.Details,
                log.OccurredAtUtc))
            .ToListAsync(cancellationToken);

        return logs
            .OrderByDescending(log => log.OccurredAtUtc)
            .Take(maxRows)
            .ToList();
    }
}
