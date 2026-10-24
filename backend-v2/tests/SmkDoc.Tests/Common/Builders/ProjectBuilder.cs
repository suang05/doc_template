using SmkDoc.Domain.Entities;
using SmkDoc.Domain.ValueObjects;

namespace SmkDoc.Tests.Common.Builders;

public class ProjectBuilder
{
    private Guid _id = Guid.NewGuid();
    private Guid _companyId = Guid.NewGuid();
    private string _name = "Sample Project";
    private string _slug = "sample-project";
    private bool _isActive = true;
    private DateTimeOffset _now = TestConstants.BaselineTime;

    public static ProjectBuilder AProject() => new();

    public static Project Default() => AProject().Build();


    public ProjectBuilder WithId(Guid id)
    {
        _id = id;
        return this;
    }

    public ProjectBuilder WithCompanyId(Guid companyId)
    {
        _companyId = companyId;
        return this;
    }

    public ProjectBuilder WithName(string name)
    {
        _name = name;
        return this;
    }

    public ProjectBuilder WithSlug(string slug)
    {
        _slug = slug;
        return this;
    }

    public ProjectBuilder AsInactive()
    {
        _isActive = false;
        return this;
    }

    public ProjectBuilder WithTime(DateTimeOffset now)
    {
        _now = now;
        return this;
    }

    public Project Build()
    {
        var project = new Project(
            new SmkDoc.Domain.ValueObjects.Ids.ProjectId(_id),
            new SmkDoc.Domain.ValueObjects.Ids.CompanyId(_companyId),
            ProjectName.Create(_name),
            ProjectSlug.Create(_slug),
            _now);

        if (!_isActive)
        {
            project.Deactivate(_now);
        }

        return project;
    }

    public static implicit operator Project(ProjectBuilder builder) => builder.Build();
}

public static class aProject
{
    public static ProjectBuilder Active() => ProjectBuilder.AProject();
    public static ProjectBuilder Inactive() => ProjectBuilder.AProject().AsInactive();
}
