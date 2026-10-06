using SmkDoc.Application.Modules.IdentityAccess.Projects.Queries.ListProjects;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Enums;
using SmkDoc.Domain.ValueObjects;
using SmkDoc.Tests.Common.Builders;

namespace SmkDoc.Tests.Application.Modules.IdentityAccess.Projects.Queries.ListProjects;

public class ListProjectsUseCaseTests
{
    private readonly Mock<IProjectRepository> _projectRepoMock = new();
    private readonly Mock<IUserProjectRoleRepository> _roleRepoMock = new();

    private ListProjectsUseCase CreateSut() =>
        new(_projectRepoMock.Object, _roleRepoMock.Object);

    [Fact]
    public async Task ExecuteAsync_WhenCalled_ReturnsOnlyActiveProjectsForUser()
    {
        // Arrange
        var now = TestConstants.BaselineTime;
        var userId = Guid.NewGuid();
        var p1 = new ProjectBuilder().WithName("Proj 1").WithSlug("proj-1").WithTime(now).Build();
        var p2 = new ProjectBuilder().WithName("Proj 2").WithSlug("proj-2").AsInactive().WithTime(now).Build();

        var roles = new List<UserProjectRole>
        {
            UserProjectRole.Create(userId, p1.Id, RoleType.Admin, now),
            UserProjectRole.Create(userId, p2.Id, RoleType.Viewer, now)
        };

        _roleRepoMock.Setup(r => r.ListByUserAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(roles);
        _projectRepoMock.Setup(r => r.ListByIdsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Project> { p1, p2 });

        // Act
        var result = (await CreateSut().ExecuteAsync(new ListProjectsQuery(userId))).ToList();

        // Assert
        result.Should().HaveCount(1);
        result[0].Id.Should().Be(p1.Id);
        result[0].Name.Should().Be("Proj 1");
    }
}
