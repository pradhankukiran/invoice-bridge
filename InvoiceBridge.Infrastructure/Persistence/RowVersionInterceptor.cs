using InvoiceBridge.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace InvoiceBridge.Infrastructure.Persistence;

public sealed class RowVersionInterceptor : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        BumpRowVersions(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        BumpRowVersions(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private static void BumpRowVersions(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        foreach (var entry in context.ChangeTracker.Entries())
        {
            if (entry.State is EntityState.Added or EntityState.Modified)
            {
                switch (entry.Entity)
                {
                    case FileImport fi:
                        BumpVersion(entry, fi.RowVersion, version => fi.RowVersion = version);
                        break;
                    case Invoice inv:
                        BumpVersion(entry, inv.RowVersion, version => inv.RowVersion = version);
                        break;
                    case ApprovalRequest ar:
                        BumpVersion(entry, ar.RowVersion, version => ar.RowVersion = version);
                        break;
                    case Payment pay:
                        BumpVersion(entry, pay.RowVersion, version => pay.RowVersion = version);
                        break;
                    case AccountingExport ae:
                        BumpVersion(entry, ae.RowVersion, version => ae.RowVersion = version);
                        break;
                }
            }
        }
    }

    private static void BumpVersion(EntityEntry entry, uint current, Action<uint> setter)
    {
        setter(entry.State == EntityState.Added ? 1u : unchecked(current + 1u));
    }
}
