namespace SmkDocServer.Domain.Entities;

public class TemplateVersion
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid TemplateId { get; set; }
    public TemplateMetadata? Template { get; set; }

    public int VersionNumber { get; set; } = 1;

    /// <summary>
    /// Relative storage path of this archived version (e.g. "versions/SMK_SALES/invoice_v1.docx")
    /// </summary>
    public string StoragePath { get; set; } = string.Empty;

    public long FileSizeBytes { get; set; }

    public int VariablesCount { get; set; }

    public string? Note { get; set; }

    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
}
