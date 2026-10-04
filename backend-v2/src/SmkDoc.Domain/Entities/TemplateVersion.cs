using SmkDoc.Domain.Enums;
using SmkDoc.Domain.Exceptions;

namespace SmkDoc.Domain.Entities;

/// <summary>
/// Domain entity representing an immutable or draft revision of a document template.
/// </summary>
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

    // Navigation property
    public virtual Template? Template { get; private set; }

    private TemplateVersion() { }

    public TemplateVersion(
        Guid templateId, 
        int version, 
        string storageKey, 
        TemplateFormat? fileFormat, 
        string? createdBy, 
        string? commitMessage = null,
        Guid? id = null) : base(id)
    {
        if (templateId == Guid.Empty)
        {
            throw new DomainValidationException("TemplateId cannot be empty.");
        }

        if (version <= 0)
        {
            throw new DomainValidationException("Template version number must be greater than zero.");
        }

        TemplateId = templateId;
        Version = version;
        StorageKey = storageKey ?? string.Empty;
        FileFormat = fileFormat;
        CreatedBy = createdBy;
        CommitMessage = commitMessage;
        Status = TemplateVersionStatus.Draft;
    }

    public RenderEngineType GetRenderEngineType()
    {
        return FileFormat?.DefaultEngineType ?? RenderEngineType.Html;
    }

    public void Publish()
    {
        if (Status == TemplateVersionStatus.Archived)
        {
            throw new BusinessRuleViolationException(
                "Cannot publish an archived template version.", 
                "ARCHIVED_VERSION_CANNOT_BE_PUBLISHED");
        }

        Status = TemplateVersionStatus.Published;
        SetUpdated();
    }

    public void Archive()
    {
        if (Status == TemplateVersionStatus.Archived)
        {
            return;
        }

        Status = TemplateVersionStatus.Archived;
        SetUpdated();
    }

    public void UpdateStorageKey(string storageKey)
    {
        if (string.IsNullOrWhiteSpace(storageKey))
        {
            throw new DomainValidationException("Storage key cannot be empty or whitespace.");
        }

        StorageKey = storageKey;
        SetUpdated();
    }

    public void UpdateDataSchema(string? schema, string? samplePayload)
    {
        if (Status == TemplateVersionStatus.Archived)
        {
            throw new BusinessRuleViolationException(
                "Cannot modify schema on an archived template version.",
                "ARCHIVED_VERSION_CANNOT_BE_MODIFIED");
        }

        DataSchema = schema;
        SamplePayload = samplePayload;
        SetUpdated();
    }

    public void UpdateMappingsSnapshot(string? snapshot)
    {
        if (Status == TemplateVersionStatus.Archived)
        {
            throw new BusinessRuleViolationException(
                "Cannot modify mappings snapshot on an archived template version.",
                "ARCHIVED_VERSION_CANNOT_BE_MODIFIED");
        }

        MappingsSnapshot = snapshot;
        SetUpdated();
    }
}
