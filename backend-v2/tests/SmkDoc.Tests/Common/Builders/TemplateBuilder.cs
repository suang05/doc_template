using SmkDoc.Domain.Entities;
using SmkDoc.Tests.Common.Factories;

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
        var now = DateTimeOffset.UtcNow;
        var template = TemplateTestFactory.Create(
            id: _id,
            projectId: _projectId,
            name: _name,
            slug: _slug,
            category: _description,
            now: now,
            isActive: _isActive,
            currentVersionId: _currentVersionId);

        return template;
    }
}
