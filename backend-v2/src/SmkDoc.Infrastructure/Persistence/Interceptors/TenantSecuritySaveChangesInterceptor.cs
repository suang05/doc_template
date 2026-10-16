using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Domain.Interfaces;

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
        }
    }
}
