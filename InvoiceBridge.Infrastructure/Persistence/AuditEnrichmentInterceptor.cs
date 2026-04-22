using InvoiceBridge.Application.Abstractions.Services;
using InvoiceBridge.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace InvoiceBridge.Infrastructure.Persistence;

public sealed class AuditEnrichmentInterceptor(IAuditContextAccessor auditContextAccessor) : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        Enrich(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        Enrich(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void Enrich(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        var ctx = auditContextAccessor.Current;
        if (ctx.IpAddress is null && ctx.UserAgent is null && ctx.CorrelationId is null)
        {
            return;
        }

        foreach (var entry in context.ChangeTracker.Entries<AuditLog>())
        {
            if (entry.State != EntityState.Added)
            {
                continue;
            }

            if (entry.Entity.IpAddress is null && ctx.IpAddress is not null)
            {
                entry.Entity.IpAddress = Truncate(ctx.IpAddress, 45);
            }

            if (entry.Entity.UserAgent is null && ctx.UserAgent is not null)
            {
                entry.Entity.UserAgent = Truncate(ctx.UserAgent, 512);
            }

            if (entry.Entity.CorrelationId is null && ctx.CorrelationId is not null)
            {
                entry.Entity.CorrelationId = Truncate(ctx.CorrelationId, 128);
            }
        }
    }

    private static string Truncate(string value, int maxLength)
        => value.Length <= maxLength ? value : value[..maxLength];
}
