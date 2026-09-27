using SmkDoc.Domain.Enums;

namespace SmkDoc.Domain.Entities;

public class TemplateVersion : BaseEntity
{
    public Guid TemplateId { get; private set; }
    public int Version { get; private set; }
    public string StorageKey { get; private set; } = string.Empty;
    public TemplateVersionStatus Status { get; private set; } = TemplateVersionStatus.Draft;
    public TemplateFormat? FileFormat { get; private set; }
    public string? DataSchema { get; private set; }
    public string? SamplePayload { get; private set; }
    public string? MappingsSnapshot { get; private set; }
    public string? CommitMessage { get; private set; }
    public string? CreatedBy { get; private set; }

    public RenderEngineType GetRenderEngineType()
    {
        return FileFormat?.DefaultEngineType ?? RenderEngineType.Html;
    }


    // Navigation property
    public virtual Template? Template { get; private set; }

    private TemplateVersion() { }

    public TemplateVersion(Guid templateId, int version, string storageKey, TemplateFormat? fileFormat, string? createdBy, string? commitMessage = null)
    {
        TemplateId = templateId;
        Version = version;
        StorageKey = storageKey;
        FileFormat = fileFormat;
        CreatedBy = createdBy;
        CommitMessage = commitMessage;
        Status = TemplateVersionStatus.Draft;
    }

    public void Publish()
    {
        Status = TemplateVersionStatus.Published;
        SetUpdated();
    }

    public void Archive()
    {
        Status = TemplateVersionStatus.Archived;
        SetUpdated();
    }

    public void UpdateDataSchema(string? schema, string? samplePayload)
    {
        DataSchema = schema;
        SamplePayload = samplePayload;
        SetUpdated();
    }

    public void UpdateMappingsSnapshot(string? snapshot)
    {
        MappingsSnapshot = snapshot;
        SetUpdated();
    }
}
