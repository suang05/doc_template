using FluentAssertions;
using Moq;
using SmkDoc.Application.Modules.IdentityAccess.Projects.Queries.ListProjects;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Enums;
using SmkDoc.Domain.Interfaces;
using SmkDoc.Tests.Common.Builders;
using SmkDoc.Tests.Common.Factories;
using Xunit;

namespace SmkDoc.Tests.Application.Modules.IdentityAccess.Projects.Queries.ListProjects;

public class ListProjectsUseCaseTests
{
    private readonly Mock<IProjectRepository> _projectRepoMock = new();
    private readonly Mock<IUserRepository> _userRepoMock = new();

    private ListProjectsUseCase CreateSut() =>
        new(_projectRepoMock.Object, _userRepoMock.Object);

    [Fact]
    public async Task ExecuteAsync_WhenStandardUser_QueriesUserAssignedProjectsOnly()
    {
        // Arrange
        var now = TestConstants.BaselineTime;
        var userId = Guid.NewGuid();
        var user = UserBuilder.AUser().WithSystemRole(SystemRole.Member).WithTime(now).Build();
        var project = ProjectTestFactory.Create(Guid.NewGuid(), Guid.NewGuid(), "Proj 1", "proj-1", now);

        _userRepoMock.Setup(u => u.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        _projectRepoMock.Setup(r => r.ListPagedByUserAsync(
                userId,
                false,
                It.IsAny<string?>(),
                1,
                20,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(([project], 1));

        // Act
        var result = await CreateSut().ExecuteAsync(new ListProjectsQuery(userId));

        // Assert
        result.Should().NotBeNull();
        result.TotalCount.Should().Be(1);
        result.Items.Should().HaveCount(1);
        result.Items[0].Id.Should().Be(project.Id);
        result.Items[0].Name.Should().Be("Proj 1");
    }

    [Fact]
    public async Task ExecuteAsync_WhenSuperAdmin_QueriesWithSuperAdminFlagTrue()
    {
        // Arrange
        var now = TestConstants.BaselineTime;
        var userId = Guid.NewGuid();
        var user = UserBuilder.AUser().WithSystemRole(SystemRole.SuperAdmin).WithTime(now).Build();
        var project = ProjectTestFactory.Create(Guid.NewGuid(), Guid.NewGuid(), "Proj 1", "proj-1", now);

        _userRepoMock.Setup(u => u.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        _projectRepoMock.Setup(r => r.ListPagedByUserAsync(
                userId,
                true,
                It.IsAny<string?>(),
                1,
                20,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(([project], 1));

        // Act
        var result = await CreateSut().ExecuteAsync(new ListProjectsQuery(userId));

        // Assert
        result.Should().NotBeNull();
        result.TotalCount.Should().Be(1);
        _projectRepoMock.Verify(r => r.ListPagedByUserAsync(
            userId,
            true,
            It.IsAny<string?>(),
            1,
            20,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_WithSearchAndPaging_ForwardsParametersToRepository()
    {
        // Arrange
        var now = TestConstants.BaselineTime;
        var userId = Guid.NewGuid();
        var user = UserBuilder.AUser().WithSystemRole(SystemRole.Member).WithTime(now).Build();

        _userRepoMock.Setup(u => u.GetByIdAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        _projectRepoMock.Setup(r => r.ListPagedByUserAsync(
                userId,
                false,
                "invoice",
                2,
                10,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(([], 0));

        // Act
        var query = new ListProjectsQuery(userId, "invoice", 2, 10);
        var result = await CreateSut().ExecuteAsync(query);

        // Assert
        result.Page.Should().Be(2);
        result.PageSize.Should().Be(10);
        result.TotalCount.Should().Be(0);
        result.Items.Should().BeEmpty();
    }
}
