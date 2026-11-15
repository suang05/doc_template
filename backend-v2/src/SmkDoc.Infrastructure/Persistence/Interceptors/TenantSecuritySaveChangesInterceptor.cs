using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using SmkDoc.Application.Common.Exceptions;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Exceptions;
using SmkDoc.Domain.Interfaces;
using SmkDoc.Domain.TenantManagement;
using SmkDoc.Domain.ValueObjects.Ids;

namespace SmkDoc.Infrastructure.Persistence.Interceptors;

/// <summary>
/// EF Core SaveChanges interceptor enforcing fail-closed tenant write isolation invariants.
/// Auto-assigns ProjectId on newly added tenant entities when empty, prevents cross-tenant writes,
/// and blocks primary-key tenant reassignment with <see cref="ErrorCodes.TenantReassignmentProhibited"/>.
/// </summary>
public sealed class TenantSecuritySaveChangesInterceptor(ICurrentProjectContext projectContext) : SaveChangesInterceptor
{
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
        if (context is null)
        {
            return;
        }

        var entries = context.ChangeTracker
            .Entries<IMustHaveProject>()
            .Where(e => e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
            .ToList();

        if (entries.Count == 0)
        {
            return;
        }

        if (!projectContext.IsResolved)
        {
            ValidateUnresolvedWriteAttempts(entries);
            return;
        }

        var currentTenantId = projectContext.GetRequiredProjectId();
        foreach (var entry in entries)
        {
            ApplyTenantRuleToEntry(entry, currentTenantId);
        }
    }

    private void ValidateUnresolvedWriteAttempts(List<EntityEntry<IMustHaveProject>> entries)
    {
        if (!projectContext.IsControlPlane)
        {
            throw new TenantContextNotResolvedException();
        }

        foreach (var entry in entries)
        {
            bool allowedInControlPlane = entry.Entity is ProjectQuota or ApiKey;
            if (!allowedInControlPlane)
            {
                throw new CrossTenantAccessDeniedException(entry.Entity.ProjectId);
            }

            if (entry.State == EntityState.Added && entry.Entity.ProjectId.IsEmpty)
            {
                throw new DomainValidationException(
                    $"ProjectId must be specified when creating {entry.Entity.GetType().Name} in Control Plane.");
            }

            if (entry.State == EntityState.Modified &&
                entry.Property(nameof(IMustHaveProject.ProjectId)).IsModified &&
                entry.Entity.ProjectId != GetOriginalProjectId(entry))
            {
                throw new ForbiddenException(
                    "Tenant reassignment is strictly prohibited: ProjectId property cannot be modified.",
                    ErrorCodes.TenantReassignmentProhibited);
            }
        }
    }

    private static void ApplyTenantRuleToEntry(
        EntityEntry<IMustHaveProject> entry,
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
        EntityEntry<IMustHaveProject> entry,
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
        EntityEntry<IMustHaveProject> entry,
        ProjectId currentTenantId)
    {
        var originalProjectId = GetOriginalProjectId(entry);
        if (originalProjectId != currentTenantId)
        {
            throw new CrossTenantAccessDeniedException(originalProjectId);
        }

        if (entry.Property(nameof(IMustHaveProject.ProjectId)).IsModified && entry.Entity.ProjectId != originalProjectId)
        {
            throw new ForbiddenException(
                $"Tenant reassignment is strictly prohibited: ProjectId property cannot be modified on entity '{entry.Entity.GetType().Name}'.",
                ErrorCodes.TenantReassignmentProhibited);
        }
    }

    private static void ValidateDeletedEntry(
        EntityEntry<IMustHaveProject> entry,
        ProjectId currentTenantId)
    {
        var originalProjectId = GetOriginalProjectId(entry);
        if (originalProjectId != currentTenantId)
        {
            throw new CrossTenantAccessDeniedException(originalProjectId);
        }
    }

    private static ProjectId GetOriginalProjectId(EntityEntry<IMustHaveProject> entry)
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
