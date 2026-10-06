using SmkDoc.Domain.Common;
using SmkDoc.Domain.Enums;
using SmkDoc.Domain.Exceptions;

namespace SmkDoc.Domain.Entities;

/// <summary>
/// Domain entity representing a version revision of a document template within the Template aggregate root.
/// </summary>
public sealed class TemplateVersion : BaseEntity
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

    // Navigation property for EF Core materialization
    public Template? Template { get; private set; }

    // For EF Core materialization only
    private TemplateVersion() { }

    internal TemplateVersion(
        Guid? id,
        Guid templateId,
        int version,
        string storageKey,
        TemplateFormat? fileFormat,
        string? createdBy,
        DateTimeOffset now,
        string? commitMessage = null)
        : base(id, createdAt: now)
    {
        TemplateId = Guard.NotEmpty(templateId, nameof(TemplateId));
        Version = Guard.Positive(version, nameof(Version));
        StorageKey = (storageKey ?? string.Empty).Trim();
        FileFormat = fileFormat;
        CreatedBy = (createdBy ?? string.Empty).Trim();
        CommitMessage = commitMessage?.Trim();
        Status = TemplateVersionStatus.Draft;
    }

    public static TemplateVersion Draft(
        Guid templateId,
        int version,
        string storageKey,
        TemplateFormat? fileFormat,
        string? createdBy,
        DateTimeOffset now,
        string? commitMessage = null) =>
        new(null, templateId, version, storageKey, fileFormat, createdBy, now, commitMessage);

    public RenderEngineType GetRenderEngineType() =>
        FileFormat?.DefaultEngineType ?? RenderEngineType.Html;

    public void Publish(DateTimeOffset now)
    {
        if (Status == TemplateVersionStatus.Archived)
        {
            throw new ArchivedVersionImmutableException(Id, "Publish");
        }

        Status = TemplateVersionStatus.Published;
        SetUpdated(now);
    }

    public void Archive(DateTimeOffset now)
    {
        if (Status == TemplateVersionStatus.Archived) return;

        Status = TemplateVersionStatus.Archived;
        SetUpdated(now);
    }

    public void UpdateStorageKey(string storageKey, DateTimeOffset now)
    {
        EnsureNotArchived("UpdateStorageKey");
        StorageKey = Guard.NotBlank(storageKey, nameof(StorageKey));
        SetUpdated(now);
    }

    public void UpdateDataSchema(string? schema, string? samplePayload, DateTimeOffset now)
    {
        EnsureNotArchived("UpdateDataSchema");
        DataSchema = schema;
        SamplePayload = samplePayload;
        SetUpdated(now);
    }

    public void UpdateMappingsSnapshot(string? snapshot, DateTimeOffset now)
    {
        EnsureNotArchived("UpdateMappingsSnapshot");
        MappingsSnapshot = snapshot;
        SetUpdated(now);
    }

    private void EnsureNotArchived(string operation)
    {
        if (Status == TemplateVersionStatus.Archived)
        {
            throw new ArchivedVersionImmutableException(Id, operation);
        }
    }
}
