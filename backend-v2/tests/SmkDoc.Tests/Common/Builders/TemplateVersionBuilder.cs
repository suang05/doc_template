using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Enums;
using SmkDoc.Domain.ValueObjects.Ids;

namespace SmkDoc.Tests.Common.Builders;

public class TemplateVersionBuilder
{
    private Guid _id = Guid.NewGuid();
    private Guid _templateId = Guid.NewGuid();
    private int _version = 1;
    private string _storageKey = "templates/sample.html";
    private TemplateFormat _format = TemplateFormat.Html;
    private string _status = "Published";
    private string? _createdBy = "system";
    private string? _commitMessage = "Initial commit";
    private DateTimeOffset _now = TestConstants.BaselineTime;

    public static TemplateVersionBuilder AVersion() => new();

    public static TemplateVersion Default() => AVersion().Build();

    public static TemplateVersion PublishedHtml(
        TemplateId templateId,
        int version = 1,
        string storageKey = "templates/sample.html",
        Guid? id = null) =>
        PublishedHtml(templateId.Value, version, storageKey, id);

    public static TemplateVersion PublishedHtml(
        Guid? templateId = null,
        int version = 1,
        string storageKey = "templates/sample.html",
        Guid? id = null) =>
        new TemplateVersionBuilder()
            .WithId(id ?? Guid.NewGuid())
            .WithTemplateId(templateId ?? Guid.NewGuid())
            .WithVersion(version)
            .WithStorageKey(storageKey)
            .WithFormat(TemplateFormat.Html)
            .WithStatus("Published")
            .Build();

    public static TemplateVersion DraftHtml(
        TemplateId templateId,
        int version = 1,
        string storageKey = "templates/sample.html",
        Guid? id = null) =>
        DraftHtml(templateId.Value, version, storageKey, id);

    public static TemplateVersion DraftHtml(
        Guid? templateId = null,
        int version = 1,
        string storageKey = "templates/sample.html",
        Guid? id = null) =>
        new TemplateVersionBuilder()
            .WithId(id ?? Guid.NewGuid())
            .WithTemplateId(templateId ?? Guid.NewGuid())
            .WithVersion(version)
            .WithStorageKey(storageKey)
            .WithFormat(TemplateFormat.Html)
            .WithStatus("Draft")
            .Build();


    public TemplateVersionBuilder WithId(Guid id)
    {
        _id = id;
        return this;
    }

    public TemplateVersionBuilder WithTemplateId(TemplateId templateId)
    {
        _templateId = templateId.Value;
        return this;
    }

    public TemplateVersionBuilder WithTemplateId(Guid templateId)
    {
        _templateId = templateId;
        return this;
    }

    public TemplateVersionBuilder WithVersion(int version)
    {
        _version = version;
        return this;
    }

    public TemplateVersionBuilder WithFormat(TemplateFormat format)
    {
        _format = format;
        return this;
    }

    public TemplateVersionBuilder WithStorageKey(string storageKey)
    {
        _storageKey = storageKey;
        return this;
    }

    public TemplateVersionBuilder WithStatus(string status)
    {
        _status = status;
        return this;
    }

    public TemplateVersionBuilder WithCreatedBy(string? createdBy)
    {
        _createdBy = createdBy;
        return this;
    }

    public TemplateVersionBuilder WithCommitMessage(string? commitMessage)
    {
        _commitMessage = commitMessage;
        return this;
    }

    public TemplateVersionBuilder WithTime(DateTimeOffset now)
    {
        _now = now;
        return this;
    }

    public TemplateVersion Build()
    {
        var version = new TemplateVersion(
            _id,
            new SmkDoc.Domain.ValueObjects.Ids.TemplateId(_templateId),
            _version,
            _storageKey,
            _format,
            _createdBy,
            _now,
            _commitMessage);

        if (_status.Equals("Published", StringComparison.OrdinalIgnoreCase) ||
            _status == TemplateVersionStatus.Published.Name)
        {
            version.Publish(_now);
        }
        else if (_status.Equals("Archived", StringComparison.OrdinalIgnoreCase) ||
                 _status == TemplateVersionStatus.Archived.Name)
        {
            version.Archive(_now);
        }

        return version;
    }

    public static implicit operator TemplateVersion(TemplateVersionBuilder builder) => builder.Build();
}
