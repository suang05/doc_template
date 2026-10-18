using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Domain.Interfaces;
using SmkDoc.Domain.ValueObjects.Ids;

namespace SmkDoc.Infrastructure.Persistence.Interceptors;

/// <summary>
/// EF Core SaveChanges interceptor enforcing tenant isolation invariants.
/// Auto-assigns ProjectId on newly added IMustHaveProject entities when empty,
/// and rejects cross-tenant writes with an InvalidOperationException.
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

        var currentTenantId = _projectContext.ProjectId;

        foreach (var entry in context.ChangeTracker.Entries<IMustHaveProject>())
        {
            if (entry.State == EntityState.Added)
            {
                if (entry.Entity.ProjectId.IsEmpty)
                {
                    entry.Property(nameof(IMustHaveProject.ProjectId)).CurrentValue = currentTenantId;
                }
                else if (entry.Entity.ProjectId != currentTenantId)
                {
                    throw new InvalidOperationException(
                        $"Cross-tenant write operation rejected: Entity belongs to project '{entry.Entity.ProjectId}', but active tenant context is '{currentTenantId}'.");
                }
            }
            else if (entry.State == EntityState.Modified)
            {
                var originalProjectId = GetOriginalProjectId(entry);

                if (originalProjectId != currentTenantId)
                {
                    throw new InvalidOperationException(
                        $"Cross-tenant update operation rejected: Entity belongs to project '{originalProjectId}', but active tenant context is '{currentTenantId}'.");
                }

                if (entry.Property(nameof(IMustHaveProject.ProjectId)).IsModified && entry.Entity.ProjectId != originalProjectId)
                {
                    throw new InvalidOperationException(
                        $"Tenant reassignment is strictly prohibited: ProjectId property cannot be modified on entity '{entry.Entity.GetType().Name}'.");
                }
            }
            else if (entry.State == EntityState.Deleted)
            {
                var originalProjectId = GetOriginalProjectId(entry);

                if (originalProjectId != currentTenantId)
                {
                    throw new InvalidOperationException(
                        $"Cross-tenant delete operation rejected: Entity belongs to project '{originalProjectId}', but active tenant context is '{currentTenantId}'.");
                }
            }
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
