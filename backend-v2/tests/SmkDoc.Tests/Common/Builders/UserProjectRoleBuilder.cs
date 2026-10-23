using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Enums;
using SmkDoc.Domain.ValueObjects.Ids;

namespace SmkDoc.Tests.Common.Builders;

public class UserProjectRoleBuilder
{
    private Guid _userId = Guid.NewGuid();
    private Guid _projectId = Guid.NewGuid();
    private RoleType _role = RoleType.Viewer;
    private DateTimeOffset _now = TestConstants.BaselineTime;

    public static UserProjectRoleBuilder ARole() => new();

    public static UserProjectRole Default() => ARole().Build();

    public UserProjectRoleBuilder ForUser(UserId userId)
    {
        _userId = userId.Value;
        return this;
    }

    public UserProjectRoleBuilder ForUser(Guid userId)
    {
        _userId = userId;
        return this;
    }

    public UserProjectRoleBuilder InProject(ProjectId projectId)
    {
        _projectId = projectId.Value;
        return this;
    }

    public UserProjectRoleBuilder InProject(Guid projectId)
    {
        _projectId = projectId;
        return this;
    }


    public UserProjectRoleBuilder WithTime(DateTimeOffset time)
    {
        _now = time;
        return this;
    }

    public UserProjectRoleBuilder WithRole(RoleType role)
    {
        _role = role;
        return this;
    }

    public UserProjectRoleBuilder AsAdmin()
    {
        _role = RoleType.Admin;
        return this;
    }

    public UserProjectRoleBuilder AsDeveloper()
    {
        _role = RoleType.Developer;
        return this;
    }

    public UserProjectRoleBuilder AsViewer()
    {
        _role = RoleType.Viewer;
        return this;
    }

    public UserProjectRole Build()
    {
        return UserProjectRole.Create(
            new SmkDoc.Domain.ValueObjects.Ids.UserId(_userId),
            new SmkDoc.Domain.ValueObjects.Ids.ProjectId(_projectId),
            _role,
            _now);
    }

    public static implicit operator UserProjectRole(UserProjectRoleBuilder builder) => builder.Build();
}
