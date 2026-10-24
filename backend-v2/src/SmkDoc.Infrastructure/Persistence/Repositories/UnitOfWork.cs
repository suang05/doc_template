using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Domain.Common;
using SmkDoc.Domain.Interfaces;

namespace SmkDoc.Infrastructure.Persistence.Repositories;

public sealed class UnitOfWork(
    AppDbContext context,
    IDomainEventDispatcher? dispatcher = null) : IUnitOfWork
{
    public async Task<int> CommitAsync(CancellationToken cancellationToken = default)
    {
        var entitiesWithEvents = context.ChangeTracker
            .Entries<IHaveDomainEvents>()
            .Where(e => e.Entity.DomainEvents.Count > 0)
            .Select(e => e.Entity)
            .ToList();

        var domainEvents = entitiesWithEvents
            .SelectMany(e => e.DomainEvents)
            .ToList();

        var result = await context.SaveChangesAsync(cancellationToken);

        foreach (var entity in entitiesWithEvents)
        {
            entity.ClearDomainEvents();
        }

        if (dispatcher != null && domainEvents.Count > 0)
        {
            await dispatcher.DispatchEventsAsync(domainEvents, cancellationToken);
        }

        return result;
    }
}
