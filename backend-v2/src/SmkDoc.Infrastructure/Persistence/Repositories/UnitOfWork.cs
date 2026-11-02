using SmkDoc.Domain.Common;
using SmkDoc.Domain.Interfaces;

namespace SmkDoc.Infrastructure.Persistence.Repositories;

/// <summary>
/// Single post-commit coordinator committing tracked EF Core mutations and dispatching
/// harvested domain events strictly after database transaction durability is achieved.
/// </summary>
public sealed class UnitOfWork(
    AppDbContext dbContext,
    IDomainEventDispatcher? domainEventDispatcher = null) : IUnitOfWork
{
    public async Task<int> CommitAsync(CancellationToken ct = default)
    {
        var entitiesWithEvents = dbContext.ChangeTracker
            .Entries<IHaveDomainEvents>()
            .Where(entry => entry.Entity.DomainEvents.Count > 0)
            .Select(entry => entry.Entity)
            .ToList();

        var pendingEvents = entitiesWithEvents
            .SelectMany(entity => entity.DomainEvents)
            .ToList();

        var affectedRows = await dbContext.SaveChangesAsync(ct);

        foreach (var entity in entitiesWithEvents)
        {
            entity.ClearDomainEvents();
        }

        if (domainEventDispatcher is not null && pendingEvents.Count > 0)
        {
            await domainEventDispatcher.DispatchEventsAsync(pendingEvents, ct);
        }

        return affectedRows;
    }
}
