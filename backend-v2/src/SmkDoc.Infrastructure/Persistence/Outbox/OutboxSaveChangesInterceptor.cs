using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using SmkDoc.Domain.Common;
using SmkDoc.Domain.Interfaces;

namespace SmkDoc.Infrastructure.Persistence.Outbox;

/// <summary>
/// Harvests pending <see cref="IDomainEvent"/> instances from tracked aggregates during EF Core SaveChanges
/// and appends durable <see cref="OutboxMessage"/> records in the exact same ACID database transaction.
/// </summary>
public sealed class OutboxSaveChangesInterceptor(
    TimeProvider timeProvider) : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        EnqueueOutboxMessages(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        EnqueueOutboxMessages(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    internal void EnqueueOutboxMessages(DbContext? context)
    {
        if (context is not AppDbContext dbContext)
        {
            return;
        }

        var now = timeProvider.GetUtcNow();
        var aggregates = dbContext.ChangeTracker
            .Entries<IHaveDomainEvents>()
            .Where(e => e.Entity.DomainEvents.Count > 0)
            .Select(e => e.Entity)
            .ToList();

        foreach (var aggregate in aggregates)
        {
            Guid? projectId = aggregate is IMustHaveProject tenantEntity && !tenantEntity.ProjectId.IsEmpty
                ? tenantEntity.ProjectId.Value
                : null;

            foreach (var domainEvent in aggregate.DomainEvents)
            {
                var (eventType, payloadJson) = OutboxEventRegistry.Serialize(domainEvent);
                dbContext.OutboxMessages.Add(OutboxMessage.Create(
                    Guid.CreateVersion7(),
                    projectId,
                    eventType,
                    payloadJson,
                    now));
            }

            aggregate.ClearDomainEvents();
        }
    }
}
