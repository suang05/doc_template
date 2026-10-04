using SmkDoc.Domain.Exceptions;
using SmkDoc.Domain.Interfaces;

namespace SmkDoc.Domain.Entities;

/// <summary>
/// Domain entity representing a cryptographic API key credential for machine-to-machine access.
/// </summary>
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

    // For EF Core materialization
    private ApiKey() { }

    public ApiKey(Guid projectId, string name, string? callerApp, string keyHash, DateTimeOffset? expiresAt, Guid? id = null)
        : base(id)
    {
        if (projectId == Guid.Empty)
        {
            throw new DomainValidationException("ProjectId cannot be empty.");
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainValidationException("ApiKey name cannot be empty or whitespace.");
        }

        if (string.IsNullOrWhiteSpace(keyHash))
        {
            throw new DomainValidationException("KeyHash cannot be empty or whitespace.");
        }

        ProjectId = projectId;
        Name = name.Trim();
        CallerApp = callerApp?.Trim() ?? string.Empty;
        KeyHash = keyHash.Trim();
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
        if (!IsActive) return;
        IsActive = false;
        SetUpdated();
    }
}
