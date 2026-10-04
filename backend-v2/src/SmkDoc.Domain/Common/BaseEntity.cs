namespace SmkDoc.Domain.Entities;

public abstract class BaseEntity
{
    public Guid Id { get; protected set; } = Guid.CreateVersion7();
    public DateTimeOffset CreatedAt { get; protected set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? UpdatedAt { get; protected set; }

    protected BaseEntity() { }

    protected BaseEntity(Guid? id, DateTimeOffset? createdAt = null)
    {
        if (id.HasValue && id.Value != Guid.Empty)
        {
            Id = id.Value;
        }

        if (createdAt.HasValue)
        {
            CreatedAt = createdAt.Value;
        }
    }

    protected void SetUpdated(DateTimeOffset? updatedAt = null) =>
        UpdatedAt = updatedAt ?? DateTimeOffset.UtcNow;

    public void SetAuditTimestampsForTesting(DateTimeOffset createdAt, DateTimeOffset? updatedAt = null)
    {
        CreatedAt = createdAt;
        UpdatedAt = updatedAt;
    }
}
