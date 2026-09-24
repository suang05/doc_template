using SmkDoc.Domain.Enums;

namespace SmkDoc.Domain.Entities;

public class TemplateVersion
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TemplateId { get; set; }
    public int Version { get; set; }
    public string StorageKey { get; set; } = string.Empty;
    public TemplateVersionStatus Status { get; set; } = TemplateVersionStatus.Draft;
    public TemplateFormat? FileFormat { get; set; }
    public string? DataSchema { get; set; }
    public string? SamplePayload { get; set; }
    public string? MappingsSnapshot { get; set; }
    public string? CommitMessage { get; set; }
    public string? CreatedBy { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public RenderEngineType GetRenderEngineType() =>
        FileFormat switch
        {
            TemplateFormat.Xlsx => RenderEngineType.Excel,
            TemplateFormat.Docx => RenderEngineType.Docx,
            _ => RenderEngineType.Html
        };

    // Navigation property
    public Template? Template { get; set; }
}
