using System.Globalization;
using Microsoft.EntityFrameworkCore;
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
        if (context is null || string.IsNullOrWhiteSpace(concurrencyContext.IfMatch) ||
            concurrencyContext.IfMatch == "*" || concurrencyContext.IsVerified)
        {
            return;
        }

        var entry = context.ChangeTracker.Entries<IConcurrencyAware>()
            .FirstOrDefault(e => e.State is EntityState.Modified or EntityState.Deleted);

        if (entry is null)
        {
            return;
        }

        var originalUpdated = entry.Metadata.FindProperty(nameof(IConcurrencyAware.UpdatedAt)) is not null
            ? entry.Property<DateTimeOffset?>(nameof(IConcurrencyAware.UpdatedAt)).OriginalValue
            : null;
        var originalCreated = entry.Property<DateTimeOffset>(nameof(IConcurrencyAware.CreatedAt)).OriginalValue;
        var originalToken = (originalUpdated ?? originalCreated).ToUnixTimeMilliseconds().ToString(CultureInfo.InvariantCulture);

        var alternateToken = entry.Entity is Template template
            ? entry.Property<Guid?>(nameof(Template.CurrentVersionId)).OriginalValue?.ToString() ?? template.CurrentVersion?.Version.ToString(CultureInfo.InvariantCulture)
            : entry.Entity.GetAlternateConcurrencyToken();

        concurrencyContext.EnsureTokenMatches(originalToken, entry.Entity.GetType().Name, entry.Entity.EntityId, alternateToken);
    }
}

