using SmkDoc.Domain.Interfaces;

namespace SmkDoc.Domain.Entities;

public class ApiKey : BaseEntity, IMustHaveProject
{
    public Guid ProjectId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string CallerApp { get; private set; } = string.Empty;
    public string KeyHash { get; private set; } = string.Empty;
    public bool IsActive { get; private set; } = true;
    public DateTimeOffset? ExpiresAt { get; private set; }
    public DateTimeOffset? LastUsedAt { get; private set; }

    // Navigation properties
    public virtual Project? Project { get; private set; }

    // For EF Core
    private ApiKey() { }

    public ApiKey(Guid projectId, string name, string? callerApp, string keyHash, DateTimeOffset? expiresAt)
    {
        ProjectId = projectId;
        Name = name;
        CallerApp = callerApp ?? string.Empty;
        KeyHash = keyHash;
        ExpiresAt = expiresAt;
        IsActive = true;
    }

    public void RecordUsage()
    {
        LastUsedAt = DateTimeOffset.UtcNow;
        SetUpdated();
    }

    public void Revoke()
    {
        IsActive = false;
        SetUpdated();
    }
}
