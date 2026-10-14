using LegendsTeamVN.Core.Identity.Abstractions;
using LegendsTeamVN.Core.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace LegendsTeamVN.Core.Persistence.Interceptors;

public class AuditableEntityInterceptor(ICurrentUserService currentUserService) : SaveChangesInterceptor
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

        foreach (var entry in context.ChangeTracker.Entries<IAuditable>())
        {
            var hasCreatedOnUtc = entry.Metadata.FindProperty(nameof(IAuditable.CreatedOnUtc)) != null;
            var hasCreatedBy = entry.Metadata.FindProperty(nameof(IAuditable.CreatedBy)) != null;
            var hasModifiedOnUtc = entry.Metadata.FindProperty(nameof(IAuditable.ModifiedOnUtc)) != null;
            var hasModifiedBy = entry.Metadata.FindProperty(nameof(IAuditable.ModifiedBy)) != null;

            if (entry.State == EntityState.Added)
            {
                if (hasCreatedOnUtc) entry.Property(nameof(IAuditable.CreatedOnUtc)).CurrentValue = DateTimeOffset.UtcNow;
                if (hasCreatedBy) entry.Property(nameof(IAuditable.CreatedBy)).CurrentValue = currentUserService.UserName ?? "System";
            }

            if (entry.State == EntityState.Modified || entry.HasChangedOwnedEntities())
            {
                if (hasModifiedOnUtc) entry.Property(nameof(IAuditable.ModifiedOnUtc)).CurrentValue = DateTimeOffset.UtcNow;
                if (hasModifiedBy) entry.Property(nameof(IAuditable.ModifiedBy)).CurrentValue = currentUserService.UserName ?? "System";
            }
        }
    }
}

public static class Extension
{
    public static bool HasChangedOwnedEntities(this EntityEntry entry) =>
        entry.References.Any(r =>
            r.TargetEntry != null &&
            r.TargetEntry.Metadata.IsOwned() &&
            (r.TargetEntry.State == EntityState.Added || r.TargetEntry.State == EntityState.Modified));
}
