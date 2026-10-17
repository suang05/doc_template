using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Enums;

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

    public static TemplateVersion Create(
        Guid? id = null,
        Guid? templateId = null,
        int version = 1,
        string storageKey = "templates/invoice.html",
        TemplateFormat? format = null,
        string status = "Published",
        string? commitMessage = "Initial commit",
        DateTimeOffset? now = null,
        string? createdBy = null)
    {
        var builder = new TemplateVersionBuilder();
        if (id.HasValue) builder.WithId(id.Value);
        if (templateId.HasValue) builder.WithTemplateId(templateId.Value);
        builder.WithVersion(version);
        builder.WithStorageKey(storageKey);
        if (format != null) builder.WithFormat(format);
        builder.WithStatus(status);
        if (commitMessage != null) builder.WithCommitMessage(commitMessage);
        if (createdBy != null) builder.WithCreatedBy(createdBy);
        if (now.HasValue) builder.WithTime(now.Value);
        return builder.Build();
    }

    public TemplateVersionBuilder WithId(Guid id)
    {
        _id = id;
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
            _templateId,
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
}
