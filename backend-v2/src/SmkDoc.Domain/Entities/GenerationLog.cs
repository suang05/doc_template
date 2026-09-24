namespace SmkDoc.Domain.Entities;

public class GenerationLog
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? TemplateId { get; set; }
    public Guid? TemplateVersionId { get; set; }
    public Guid? ApiKeyId { get; set; }
    public string? CallerApp { get; set; }
    public string? TriggerSource { get; set; }
    public string? InputData { get; set; }
    public string? OutputKey { get; set; }
    public string? OutputFormat { get; set; }
    public long? FileSizeBytes { get; set; }
    public int? PageCount { get; set; }
    public string? PayloadHashSha256 { get; set; }
    public int DurationMs { get; set; }
    public string Status { get; set; } = "SUCCESS";
    public string? ErrorMsg { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    // Navigation properties
    public Template? Template { get; set; }
    public TemplateVersion? TemplateVersion { get; set; }
    public ApiKey? ApiKey { get; set; }
}
