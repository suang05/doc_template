using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Domain.Exceptions;
using SmkDoc.Domain.Interfaces;
using SmkDoc.Domain.ValueObjects.Ids;

namespace SmkDoc.Infrastructure.Persistence.Interceptors;

/// <summary>
/// EF Core SaveChanges interceptor enforcing tenant isolation invariants.
/// Auto-assigns ProjectId on newly added IMustHaveProject entities when empty,
/// and rejects cross-tenant writes with a CrossTenantAccessDeniedException.
/// </summary>
public sealed class TenantSecuritySaveChangesInterceptor(ICurrentProjectContext projectContext) : SaveChangesInterceptor
{
    private readonly ICurrentProjectContext _projectContext = projectContext;

    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        ApplyTenantSecurity(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        ApplyTenantSecurity(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    internal void ApplyTenantSecurity(DbContext? context)
    {
        if (context == null || !_projectContext.IsResolved)
        {
            return;
        }

        var currentTenantId = _projectContext.GetRequiredProjectId();

        foreach (var entry in context.ChangeTracker.Entries<IMustHaveProject>())
        {
            ApplyTenantRuleToEntry(entry, currentTenantId);
        }
    }

    private static void ApplyTenantRuleToEntry(
        Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry<IMustHaveProject> entry,
        ProjectId currentTenantId)
    {
        switch (entry.State)
        {
            case EntityState.Added:
                ValidateAddedEntry(entry, currentTenantId);
                break;
            case EntityState.Modified:
                ValidateModifiedEntry(entry, currentTenantId);
                break;
            case EntityState.Deleted:
                ValidateDeletedEntry(entry, currentTenantId);
                break;
        }
    }

    private static void ValidateAddedEntry(
        Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry<IMustHaveProject> entry,
        ProjectId currentTenantId)
    {
        if (entry.Entity.ProjectId.IsEmpty)
        {
            entry.Property(nameof(IMustHaveProject.ProjectId)).CurrentValue = currentTenantId;
            return;
        }

        if (entry.Entity.ProjectId != currentTenantId)
        {
            throw new CrossTenantAccessDeniedException(entry.Entity.ProjectId);
        }
    }

    private static void ValidateModifiedEntry(
        Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry<IMustHaveProject> entry,
        ProjectId currentTenantId)
    {
        var originalProjectId = GetOriginalProjectId(entry);
        if (originalProjectId != currentTenantId)
        {
            throw new CrossTenantAccessDeniedException(originalProjectId);
        }

        if (entry.Property(nameof(IMustHaveProject.ProjectId)).IsModified && entry.Entity.ProjectId != originalProjectId)
        {
            throw new InvalidOperationException(
                $"Tenant reassignment is strictly prohibited: ProjectId property cannot be modified on entity '{entry.Entity.GetType().Name}'.");
        }
    }

    private static void ValidateDeletedEntry(
        Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry<IMustHaveProject> entry,
        ProjectId currentTenantId)
    {
        var originalProjectId = GetOriginalProjectId(entry);
        if (originalProjectId != currentTenantId)
        {
            throw new CrossTenantAccessDeniedException(originalProjectId);
        }
    }

    private static ProjectId GetOriginalProjectId(Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry<IMustHaveProject> entry)
    {
        var raw = entry.Property(nameof(IMustHaveProject.ProjectId)).OriginalValue;
        return raw switch
        {
            ProjectId pid => pid,
            Guid gid => new ProjectId(gid),
            _ => entry.Entity.ProjectId
        };
    }
}
