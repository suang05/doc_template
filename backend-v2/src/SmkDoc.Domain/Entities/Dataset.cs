namespace SmkDoc.Domain.Entities;

public class Dataset
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid DataConnectionId { get; set; }
    public string SqlQuery { get; set; } = string.Empty;
    public int CacheSeconds { get; set; } = 0;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? UpdatedAt { get; set; }

    // Navigation
    public DataConnection? DataConnection { get; set; }
}
