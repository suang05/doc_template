using SmkDoc.Domain.Entities;

namespace SmkDoc.Tests.Domain.Entities;

public class UserProjectRoleTests
{
    private readonly DateTimeOffset _initialTime = TestConstants.BaselineTime;

    [Fact]
    public void Create_WithValidParameters_InitializesCorrectly()
    {
        var userId = Guid.NewGuid();
        var projectId = Guid.NewGuid();

        var role = UserProjectRole.Create(userId, projectId, RoleType.Developer, _initialTime);

        role.UserId.Should().Be(userId);
        role.ProjectId.Should().Be(projectId);
        role.Role.Should().Be(RoleType.Developer);
        role.CreatedAt.Should().Be(_initialTime);
    }

    [Fact]
    public void Create_WithEmptyUserId_ThrowsDomainValidationException()
    {
        var act = () => UserProjectRole.Create(Guid.Empty, Guid.NewGuid(), RoleType.Viewer, _initialTime);

        act.Should().Throw<DomainValidationException>()
            .WithMessage("*UserId cannot be empty*");
    }

    [Fact]
    public void Create_WithEmptyProjectId_ThrowsDomainValidationException()
    {
        var act = () => UserProjectRole.Create(Guid.NewGuid(), Guid.Empty, RoleType.Viewer, _initialTime);

        act.Should().Throw<DomainValidationException>()
            .WithMessage("*ProjectId cannot be empty*");
    }
}
