using SmkDoc.Application.Common.Interfaces;
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
    private readonly IProjectQueryService _projectQueryService = Substitute.For<IProjectQueryService>();
    private readonly IUserRepository _userRepo = Substitute.For<IUserRepository>();

    private ListProjectsUseCase CreateSut() => new(_projectQueryService, _userRepo);

    [Fact]
    public async Task ExecuteAsync_WhenStandardUser_QueriesUserAssignedProjectsOnly()
    {
        // Arrange
        var now = TestConstants.BaselineTime;
        var userId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        User user = UserBuilder.AUser().WithSystemRole(SystemRole.Member).WithTime(now);
        var projectDto = new ProjectResultDto(projectId, "Proj 1", "proj-1", true, now);

        _userRepo.GetByIdAsync(new UserId(userId), Arg.Any<CancellationToken>())
            .Returns(user);

        _projectQueryService.ListPagedByUserAsync(
                new UserId(userId),
                false,
                Arg.Any<string?>(),
                1,
                20,
                Arg.Any<CancellationToken>())
            .Returns(([projectDto], 1));

        // Act
        ProjectPagedResultDto result = await CreateSut().ExecuteAsync(new ListProjectsQuery(userId));

        // Assert
        result.Should().NotBeNull();
        result.TotalCount.Should().Be(1);
        result.Items.Should().HaveCount(1);
        result.Items[0].Id.Should().Be(projectId);
        result.Items[0].Name.Should().Be("Proj 1");
    }

    [Fact]
    public async Task ExecuteAsync_WhenSuperAdmin_QueriesWithSuperAdminFlagTrue()
    {
        // Arrange
        var now = TestConstants.BaselineTime;
        var userId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        User user = UserBuilder.AUser().WithSystemRole(SystemRole.SuperAdmin).WithTime(now);
        var projectDto = new ProjectResultDto(projectId, "Proj 1", "proj-1", true, now);

        _userRepo.GetByIdAsync(new UserId(userId), Arg.Any<CancellationToken>())
            .Returns(user);

        _projectQueryService.ListPagedByUserAsync(
                new UserId(userId),
                true,
                Arg.Any<string?>(),
                1,
                20,
                Arg.Any<CancellationToken>())
            .Returns(([projectDto], 1));

        // Act
        ProjectPagedResultDto result = await CreateSut().ExecuteAsync(new ListProjectsQuery(userId));

        // Assert
        result.Should().NotBeNull();
        result.TotalCount.Should().Be(1);
        await _projectQueryService.Received(1).ListPagedByUserAsync(
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

        _projectQueryService.ListPagedByUserAsync(
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
