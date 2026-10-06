using SmkDoc.Domain.Entities;

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
