using FluentAssertions;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Enums;
using SmkDoc.Domain.Exceptions;
using SmkDoc.Domain.ValueObjects.Ids;
using Xunit;

namespace SmkDoc.Tests.Domain.Entities;

public sealed class UserProjectRoleTests
{
    private readonly DateTimeOffset _initialTime = TestConstants.BaselineTime;

    [Fact]
    public void Create_WithValidParameters_InitializesCorrectly()
    {
        var userId = UserId.New();
        var projectId = ProjectId.New();

        var role = UserProjectRole.Create(userId, projectId, RoleType.Developer, _initialTime);

        role.UserId.Should().Be(userId);
        role.ProjectId.Should().Be(projectId);
        role.Role.Should().Be(RoleType.Developer);
        role.CreatedAt.Should().Be(_initialTime);
    }

    [Fact]
    public void Create_WithEmptyUserId_ThrowsDomainValidationException()
    {
        var act = () => UserProjectRole.Create(UserId.Empty, ProjectId.New(), RoleType.Viewer, _initialTime);

        act.Should().Throw<DomainValidationException>()
            .WithMessage("*UserId cannot be empty*");
    }

    [Fact]
    public void Create_WithEmptyProjectId_ThrowsDomainValidationException()
    {
        var act = () => UserProjectRole.Create(UserId.New(), ProjectId.Empty, RoleType.Viewer, _initialTime);

        act.Should().Throw<DomainValidationException>()
            .WithMessage("*ProjectId cannot be empty*");
    }
}
