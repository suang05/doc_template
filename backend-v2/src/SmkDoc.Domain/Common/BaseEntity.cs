namespace SmkDoc.Domain.Entities;

public abstract class BaseEntity
{
    public Guid Id { get; init; } = Guid.CreateVersion7();
    public DateTimeOffset CreatedAt { get; protected set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? UpdatedAt { get; protected set; }
    protected void SetUpdated() => UpdatedAt = DateTimeOffset.UtcNow;
}
