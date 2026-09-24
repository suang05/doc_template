namespace SmkDocServer.Domain.Entities;

public class ApiKey
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Foreign key referencing the owning Project
    /// </summary>
    public Guid ProjectId { get; set; }

    /// <summary>
    /// Plain API Key or hashed secret (e.g. "smk_live_a1b2c3d4...")
    /// </summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>
    /// Descriptive name for this key (e.g. "Production Key", "Staging Key", "Report Runner")
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Whether the key is currently active
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Optional expiration timestamp in UTC
    /// </summary>
    public DateTime? ExpiresAt { get; set; }

    /// <summary>
    /// Timestamp of when the key was last used in UTC
    /// </summary>
    public DateTime? LastUsedAt { get; set; }

    /// <summary>
    /// Creation timestamp in UTC
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation property
    public Project? Project { get; set; }
}
