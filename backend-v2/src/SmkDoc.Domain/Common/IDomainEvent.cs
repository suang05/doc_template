namespace SmkDoc.Domain.Common;

public interface IDomainEvent
{
    Guid EventId => Guid.CreateVersion7();
    DateTimeOffset OccurredOn => DateTimeOffset.UtcNow;
}