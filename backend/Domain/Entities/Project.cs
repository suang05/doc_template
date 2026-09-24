namespace SmkDocServer.Domain.Entities;

public class Project
{
    public Guid Id { get; set; } = Guid.NewGuid();
    
    /// <summary>
    /// Unique project identifier code (e.g. "SALES_APP", "HR_PAYROLL", "SMK_PORTAL", "SMK_INTERNAL")
    /// </summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>
    /// Display name of the project / subsystem
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Optional description of the system
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Whether this project is currently active
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Creation timestamp in UTC
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public List<ApiKey> ApiKeys { get; set; } = new();
    public List<GenerationLog> GenerationLogs { get; set; } = new();
    public List<TemplateMetadata> Templates { get; set; } = new();
}
