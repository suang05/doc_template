using SmkDocServer.Domain.Models;

namespace SmkDocServer.Domain.Entities;

public class TemplateMetadata
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// ID of the Project this template belongs to.
    /// If null, this is a GLOBAL template accessible by all projects.
    /// </summary>
    public Guid? ProjectId { get; set; }
    public Project? Project { get; set; }

    /// <summary>
    /// Template file name (e.g. "invoice.docx")
    /// </summary>
    public string FileName { get; set; } = string.Empty;

    /// <summary>
    /// Original uploaded file name
    /// </summary>
    public string OriginalName { get; set; } = string.Empty;

    /// <summary>
    /// File extension (e.g. ".docx", ".xlsx")
    /// </summary>
    public string Format { get; set; } = string.Empty;

    /// <summary>
    /// Relative storage path inside Templates/ and MinIO bucket
    /// e.g. "SMK_SALES/invoice.docx" or "GLOBAL/invoice.docx"
    /// </summary>
    public string StoragePath { get; set; } = string.Empty;

    /// <summary>
    /// Whether this template is available globally to all projects
    /// </summary>
    public bool IsGlobal { get; set; } = false;

    public int CurrentVersion { get; set; } = 1;

    /// <summary>
    /// Determines which rendering engine processes this template.
    /// Defaults to OpenXML for all existing uploaded .docx / .xlsx templates.
    /// </summary>
    public TemplateEngineType EngineType { get; set; } = TemplateEngineType.OpenXML;

    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;

    public ICollection<TemplateVersion> Versions { get; set; } = new List<TemplateVersion>();
    public ICollection<FieldMapping> FieldMappings { get; set; } = new List<FieldMapping>();
}
