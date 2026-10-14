using LegendsTeamVN.Core.Identity.Abstractions;
using LegendsTeamVN.Core.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace LegendsTeamVN.Core.Persistence.Interceptors;

public class SoftDeleteInterceptor(ICurrentUserService currentUserService) : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        UpdateEntities(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        UpdateEntities(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    public void UpdateEntities(DbContext? context)
    {
        if (context == null) return;

        foreach (var entry in context.ChangeTracker.Entries<ISoftDeletable>())
        {
            var hasIsDeleted = entry.Metadata.FindProperty(nameof(ISoftDeletable.IsDeleted)) != null;
            if (entry.State == EntityState.Deleted && hasIsDeleted)
            {
                entry.State = EntityState.Modified;
                entry.Property(nameof(ISoftDeletable.IsDeleted)).CurrentValue = true;

                if (entry.Metadata.FindProperty(nameof(ISoftDeletable.DeletedOnUtc)) != null)
                    entry.Property(nameof(ISoftDeletable.DeletedOnUtc)).CurrentValue = DateTimeOffset.UtcNow;

                if (entry.Metadata.FindProperty(nameof(ISoftDeletable.DeletedBy)) != null)
                    entry.Property(nameof(ISoftDeletable.DeletedBy)).CurrentValue = currentUserService.UserName;
            }
        }
    }
}
