using InvoiceBridge.Application.Abstractions.Persistence;
using InvoiceBridge.Domain.Entities;

namespace InvoiceBridge.Application.Common;

internal static class AuditTrailWriter
{
    public static void Add(
        IApplicationDbContext dbContext,
        string entityName,
        string entityId,
        string action,
        string actor,
        string details)
    {
        dbContext.AuditLogs.Add(new AuditLog
        {
            EntityName = entityName,
            EntityId = entityId,
            Action = action,
            Actor = string.IsNullOrWhiteSpace(actor) ? "system" : actor.Trim(),
            Details = string.IsNullOrWhiteSpace(details) ? "n/a" : details.Trim()
        });
    }
}
