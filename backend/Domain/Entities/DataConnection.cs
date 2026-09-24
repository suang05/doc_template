using System;

namespace SmkDocServer.Domain.Entities;

public class DataConnection
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Name { get; set; } = string.Empty;

    public Guid? ProjectId { get; set; }
    public Project? Project { get; set; }

    /// <summary>postgresql | sqlserver | mysql</summary>
    public string DatabaseType { get; set; } = "postgresql";

    /// <summary>AES-256 / Data Protection encrypted connection string</summary>
    public string ConnectionStringEncrypted { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
