using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Enums;

namespace SmkDoc.Tests.Common.Builders;

public class UserProjectRoleBuilder
{
    private Guid _userId = Guid.NewGuid();
    private Guid _projectId = Guid.NewGuid();
    private RoleType _role = RoleType.Viewer;
    private DateTimeOffset _now = TestConstants.BaselineTime;

    public static UserProjectRoleBuilder ARole() => new();

    public UserProjectRoleBuilder ForUser(Guid userId)
    {
        _userId = userId;
        return this;
    }

    public UserProjectRoleBuilder WithUserId(Guid userId) => ForUser(userId);

    public UserProjectRoleBuilder InProject(Guid projectId)
    {
        _projectId = projectId;
        return this;
    }

    public UserProjectRoleBuilder WithProjectId(Guid projectId) => InProject(projectId);

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
        return UserProjectRole.Create(_userId, _projectId, _role, _now);
    }
}
