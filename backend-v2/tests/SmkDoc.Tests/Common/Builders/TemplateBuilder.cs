using SmkDoc.Domain.Entities;
using SmkDoc.Domain.ValueObjects;

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

    public static TemplateBuilder ATemplate() => new();

    public static Template Default() => ATemplate().Build();

    public static Template ActiveHtml(
        string name = "Sample Template",
        string slug = "sample-template",
        Guid? projectId = null,
        Guid? currentVersionId = null) =>
        new TemplateBuilder()
            .WithName(name)
            .WithSlug(slug)
            .WithProjectId(projectId ?? TestConstants.DefaultProjectId)
            .WithCurrentVersion(currentVersionId ?? Guid.NewGuid())
            .Build();

    public static Template Inactive(
        string name = "Inactive Template",
        string slug = "inactive-template",
        Guid? projectId = null) =>
        new TemplateBuilder()
            .WithName(name)
            .WithSlug(slug)
            .WithProjectId(projectId ?? TestConstants.DefaultProjectId)
            .AsInactive()
            .Build();

    public static Template Create(
        Guid? id = null,
        Guid? projectId = null,
        string name = "Sample Template",
        string slug = "sample-template",
        string? category = null,
        DateTimeOffset? now = null,
        bool isActive = true,
        Guid? currentVersionId = null)
    {
        var builder = new TemplateBuilder();
        if (id.HasValue) builder.WithId(id.Value);
        if (projectId.HasValue) builder.WithProjectId(projectId.Value);
        builder.WithName(name);
        builder.WithSlug(slug);
        if (category != null) builder.WithCategory(category);
        if (now.HasValue) builder.WithTime(now.Value);
        if (!isActive) builder.AsInactive();
        if (currentVersionId.HasValue)
        {
            builder.WithCurrentVersion(currentVersionId.Value);
        }
        else
        {
            builder.WithoutCurrentVersion();
        }
        return builder.Build();
    }

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
