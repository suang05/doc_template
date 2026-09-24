namespace SmkDocServer.Domain.Entities;

public class GenerationLog
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// ID of the Project / Subsystem that requested the document
    /// </summary>
    public Guid? ProjectId { get; set; }

    /// <summary>
    /// ID of the specific ApiKey used for authentication
    /// </summary>
    public Guid? ApiKeyId { get; set; }

    /// <summary>
    /// Template file name used (e.g. "sample_full_features.docx")
    /// </summary>
    public string TemplateName { get; set; } = string.Empty;

    /// <summary>
    /// Requested output format (e.g. "pdf", "docx", "xlsx")
    /// </summary>
    public string OutputFormat { get; set; } = string.Empty;

    /// <summary>
    /// Generated output filename stored in MinIO
    /// </summary>
    public string? OutputFileName { get; set; }

    /// <summary>
    /// File size in bytes (if successfully generated)
    /// </summary>
    public long? FileSizeBytes { get; set; }

    /// <summary>
    /// Execution duration in milliseconds
    /// </summary>
    public long ExecutionTimeMs { get; set; }

    /// <summary>
    /// Status: "SUCCESS" or "FAILED"
    /// </summary>
    public string Status { get; set; } = "SUCCESS";

    /// <summary>
    /// Detailed error message if Status is "FAILED"
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Client IP Address
    /// </summary>
    public string? IpAddress { get; set; }

    /// <summary>
    /// Client User-Agent
    /// </summary>
    public string? UserAgent { get; set; }

    /// <summary>
    /// Timestamp in UTC
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public Project? Project { get; set; }
    public ApiKey? ApiKey { get; set; }
}
