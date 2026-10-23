using SmkDoc.Application.Modules.IdentityAccess.Projects.DTOs;
using SmkDoc.Application.Modules.IdentityAccess.Projects.Queries.ListProjects;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Enums;
using SmkDoc.Domain.Interfaces;
using SmkDoc.Domain.ValueObjects.Ids;
using SmkDoc.Tests.Common.Builders;

namespace SmkDoc.Tests.Application.Modules.IdentityAccess.Projects.Queries.ListProjects;

public sealed class ListProjectsUseCaseTests
{
    private readonly IProjectRepository _projectRepo = Substitute.For<IProjectRepository>();
    private readonly IUserRepository _userRepo = Substitute.For<IUserRepository>();

    private ListProjectsUseCase CreateSut() => new(_projectRepo, _userRepo);

    [Fact]
    public async Task ExecuteAsync_WhenStandardUser_QueriesUserAssignedProjectsOnly()
    {
        // Arrange
        var now = TestConstants.BaselineTime;
        var userId = Guid.NewGuid();
        User user = UserBuilder.AUser().WithSystemRole(SystemRole.Member).WithTime(now);
        Project project = ProjectBuilder.AProject().WithName("Proj 1").WithSlug("proj-1").WithTime(now);

        _userRepo.GetByIdAsync(new UserId(userId), Arg.Any<CancellationToken>())
            .Returns(user);

        _projectRepo.ListPagedByUserAsync(
                new UserId(userId),
                false,
                Arg.Any<string?>(),
                1,
                20,
                Arg.Any<CancellationToken>())
            .Returns(([project], 1));

        // Act
        ProjectPagedResultDto result = await CreateSut().ExecuteAsync(new ListProjectsQuery(userId));

        // Assert
        result.Should().NotBeNull();
        result.TotalCount.Should().Be(1);
        result.Items.Should().HaveCount(1);
        result.Items[0].Id.Should().Be(project.Id.Value);
        result.Items[0].Name.Should().Be("Proj 1");
    }

    [Fact]
    public async Task ExecuteAsync_WhenSuperAdmin_QueriesWithSuperAdminFlagTrue()
    {
        // Arrange
        var now = TestConstants.BaselineTime;
        var userId = Guid.NewGuid();
        User user = UserBuilder.AUser().WithSystemRole(SystemRole.SuperAdmin).WithTime(now);
        Project project = ProjectBuilder.AProject().WithName("Proj 1").WithSlug("proj-1").WithTime(now);

        _userRepo.GetByIdAsync(new UserId(userId), Arg.Any<CancellationToken>())
            .Returns(user);

        _projectRepo.ListPagedByUserAsync(
                new UserId(userId),
                true,
                Arg.Any<string?>(),
                1,
                20,
                Arg.Any<CancellationToken>())
            .Returns(([project], 1));

        // Act
        ProjectPagedResultDto result = await CreateSut().ExecuteAsync(new ListProjectsQuery(userId));

        // Assert
        result.Should().NotBeNull();
        result.TotalCount.Should().Be(1);
        await _projectRepo.Received(1).ListPagedByUserAsync(
            new UserId(userId),
            true,
            Arg.Any<string?>(),
            1,
            20,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WithSearchAndPaging_ForwardsParametersToRepository()
    {
        // Arrange
        var now = TestConstants.BaselineTime;
        var userId = Guid.NewGuid();
        User user = UserBuilder.AUser().WithSystemRole(SystemRole.Member).WithTime(now);

        _userRepo.GetByIdAsync(new UserId(userId), Arg.Any<CancellationToken>())
            .Returns(user);

        _projectRepo.ListPagedByUserAsync(
                new UserId(userId),
                false,
                "invoice",
                2,
                10,
                Arg.Any<CancellationToken>())
            .Returns(([], 0));

        // Act
        var query = new ListProjectsQuery(userId, "invoice", 2, 10);
        ProjectPagedResultDto result = await CreateSut().ExecuteAsync(query);

        // Assert
        result.Page.Should().Be(2);
        result.PageSize.Should().Be(10);
        result.TotalCount.Should().Be(0);
        result.Items.Should().BeEmpty();
    }
}
