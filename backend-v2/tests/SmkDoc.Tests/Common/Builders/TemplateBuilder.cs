using SmkDoc.Domain.Entities;

namespace SmkDoc.Tests.Common.Builders;

public class TemplateBuilder
{
    private Guid _id = Guid.NewGuid();
    private Guid _projectId = TestConstants.DefaultProjectId;
    private string _name = "Sample Template";
    private string _slug = "sample-template";
    private string? _category = "General";
    private Guid? _currentVersionId = Guid.NewGuid();
    private bool _isActive = true;
    private DateTimeOffset _now = TestConstants.BaselineTime;

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

    public TemplateBuilder WithCategory(string? category)
    {
        _category = category;
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

    public TemplateBuilder WithTime(DateTimeOffset now)
    {
        _now = now;
        return this;
    }

    public Template Build()
    {
        var template = new Template(
            _id,
            _projectId,
            TemplateName.Create(_name),
            TemplateSlug.Create(_slug),
            _category,
            _now);

        if (_currentVersionId.HasValue)
        {
            template.SetCurrentVersion(_currentVersionId.Value, _now);
        }

        if (!_isActive)
        {
            template.Deactivate(_now);
        }

        return template;
    }
}
