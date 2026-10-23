namespace SmkDoc.Domain.Common;

public interface IHaveDomainEvents
{
    IReadOnlyCollection<IDomainEvent> DomainEvents { get; }
    void ClearDomainEvents();
}