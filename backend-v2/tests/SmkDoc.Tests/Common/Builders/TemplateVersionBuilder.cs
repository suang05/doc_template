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
    private string? _commitMessage = "Initial commit";

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

    public TemplateVersion Build()
    {
        return new TemplateVersion(_templateId, _version, _storageKey, _format, _status, _commitMessage)
        {
            Id = _id
        };
    }
}
