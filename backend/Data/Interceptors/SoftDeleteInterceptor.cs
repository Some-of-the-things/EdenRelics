using Eden_Relics_BE.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Eden_Relics_BE.Data.Interceptors;

/// <summary>
/// Converts every entity deletion into a soft delete: any <see cref="BaseEntity"/>
/// marked Deleted is rewritten to Modified with IsDeleted = true, so no application
/// code can permanently remove a row by mistake. Entities marked
/// <see cref="IHardDeletable"/> are exempt and deleted normally. The only way to
/// truly hard-delete a soft-deletable row is directly in the database.
///
/// Note: bulk operations (ExecuteDelete) bypass the change tracker and therefore
/// this interceptor — those remain genuine deletes by design (e.g. analytics
/// re-ingestion that clears a date range before reloading).
/// </summary>
public sealed class SoftDeleteInterceptor : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        ConvertDeletesToSoftDeletes(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        ConvertDeletesToSoftDeletes(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private static void ConvertDeletesToSoftDeletes(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        foreach (var entry in context.ChangeTracker.Entries<BaseEntity>())
        {
            if (entry.State != EntityState.Deleted || entry.Entity is IHardDeletable)
            {
                continue;
            }

            entry.State = EntityState.Modified;
            entry.Entity.IsDeleted = true;
        }
    }
}
