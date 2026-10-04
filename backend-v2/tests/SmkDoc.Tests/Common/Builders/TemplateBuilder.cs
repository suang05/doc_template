using SmkDoc.Domain.Entities;

namespace SmkDoc.Tests.Common.Builders;

public class TemplateBuilder
{
    private Guid _id = Guid.NewGuid();
    private Guid _projectId = Guid.NewGuid();
    private string _name = "Sample Template";
    private string _slug = "sample-template";
    private string? _description = "Sample Description";
    private Guid? _currentVersionId = Guid.NewGuid();
    private bool _isActive = true;

    public TemplateBuilder WithId(Guid id)
    {
        _id = id;
        return this;
    }

    public TemplateBuilder WithProjectId(Guid projectId)
    {
        _projectId = projectId;
        return this;
    }

    public TemplateBuilder WithName(string name)
    {
        _name = name;
        return this;
    }

    public TemplateBuilder WithSlug(string slug)
    {
        _slug = slug;
        return this;
    }

    public TemplateBuilder WithCurrentVersion(Guid versionId)
    {
        _currentVersionId = versionId;
        return this;
    }

    public TemplateBuilder WithoutCurrentVersion()
    {
        _currentVersionId = null;
        return this;
    }

    public TemplateBuilder AsInactive()
    {
        _isActive = false;
        return this;
    }

    public Template Build()
    {
        var template = new Template(_projectId, _name, _slug, _description, id: _id);

        if (_currentVersionId.HasValue)
        {
            template.SetCurrentVersion(_currentVersionId.Value);
        }

        if (!_isActive)
        {
            template.Deactivate();
        }

        return template;
    }
}
