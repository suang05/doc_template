using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Interfaces;

namespace SmkDoc.Infrastructure.Persistence.Interceptors;

/// <summary>
/// EF Core SaveChanges interceptor enforcing optimistic concurrency preconditions (HTTP 412)
/// against OriginalValue timestamps before committing Modified or Deleted IConcurrencyAware entities.
/// </summary>
public sealed class ConcurrencySaveChangesInterceptor(IConcurrencyContext concurrencyContext) : SaveChangesInterceptor
{
    private readonly IConcurrencyContext _concurrencyContext = concurrencyContext;

    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        ValidateConcurrency(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        ValidateConcurrency(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    internal void ValidateConcurrency(DbContext? context)
    {
        if (context is null || !_concurrencyContext.HasIfMatch || _concurrencyContext.IsWildcard || _concurrencyContext.IsVerified)
        {
            return;
        }

        foreach (var entry in context.ChangeTracker.Entries<IConcurrencyAware>())
        {
            if (entry.State is not (EntityState.Modified or EntityState.Deleted))
            {
                continue;
            }

            var originalToken = ResolveOriginalToken(entry);
            var alternateToken = ResolveAlternateToken(entry);
            var entityId = ResolveEntityId(entry);

            _concurrencyContext.EnsureTokenMatches(
                originalToken,
                entry.Entity.GetType().Name,
                entityId,
                alternateToken);

            // Once the root aggregate precondition is verified for this request, stop checking remaining entries
            return;
        }
    }

    private static string ResolveOriginalToken(EntityEntry<IConcurrencyAware> entry)
    {
        DateTimeOffset? originalUpdated = null;
        var updatedProp = entry.Metadata.FindProperty(nameof(IConcurrencyAware.UpdatedAt));
        if (updatedProp is not null)
        {
            originalUpdated = entry.Property<DateTimeOffset?>(nameof(IConcurrencyAware.UpdatedAt)).OriginalValue;
        }

        var originalCreated = entry.Property<DateTimeOffset>(nameof(IConcurrencyAware.CreatedAt)).OriginalValue;
        return (originalUpdated ?? originalCreated).ToUnixTimeMilliseconds().ToString();
    }

    private static string? ResolveAlternateToken(EntityEntry<IConcurrencyAware> entry)
    {
        if (entry.Entity is Template template)
        {
            var originalVersionId = entry.Property<Guid?>(nameof(Template.CurrentVersionId)).OriginalValue;
            return originalVersionId?.ToString() ?? template.CurrentVersion?.Version.ToString();
        }

        return entry.Entity.GetAlternateConcurrencyToken();
    }

    private static object ResolveEntityId(EntityEntry<IConcurrencyAware> entry) =>
        entry.Entity switch
        {
            Template t => t.Id.Value,
            Dataset d => d.Id.Value,
            DataConnection c => c.Id.Value,
            Project p => p.Id.Value,
            ApiKey k => k.Id.Value,
            UserProjectRole r => r.UserId.Value,
            _ => string.Empty
        };
}

