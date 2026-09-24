namespace SmkDoc.Domain.Entities;

public class ApiKey
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProjectId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string CallerApp { get; set; } = string.Empty;
    public string KeyHash { get; set; } = string.Empty; // System uses SHA-256 for fast validation
    public bool IsActive { get; set; } = true;
    public DateTimeOffset? ExpiresAt { get; set; }
    public DateTimeOffset? LastUsedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    // Navigation properties
    public Project? Project { get; set; }
}
