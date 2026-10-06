using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Enums;

namespace SmkDoc.Tests.Common.Builders;

public class UserProjectRoleBuilder
{
    private Guid _userId = Guid.NewGuid();
    private Guid _projectId = Guid.NewGuid();
    private RoleType _role = RoleType.Viewer;

    public UserProjectRoleBuilder ForUser(Guid userId)
    {
        _userId = userId;
        return this;
    }

    public UserProjectRoleBuilder InProject(Guid projectId)
    {
        _projectId = projectId;
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
        return UserProjectRole.Create(_userId, _projectId, _role, TestConstants.BaselineTime);
    }
}
