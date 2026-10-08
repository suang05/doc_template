using FluentAssertions;
using Moq;
using SmkDoc.Application.Modules.IdentityAccess.Security.Queries.GetCurrentUserProfile;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Enums;
using SmkDoc.Domain.Exceptions;
using SmkDoc.Domain.Interfaces;
using SmkDoc.Tests.Common.Builders;
using SmkDoc.Tests.Common.Factories;
using Xunit;

namespace SmkDoc.Tests.Application.Modules.IdentityAccess.Security.Queries.GetCurrentUserProfile;

public class GetCurrentUserProfileUseCaseTests
{
    private readonly Mock<IUserRepository> _userRepoMock = new();
    private readonly Mock<IUserProjectRoleRepository> _roleRepoMock = new();
    private readonly Mock<IProjectRepository> _projectRepoMock = new();

    private GetCurrentUserProfileUseCase CreateSut() => new(
        _userRepoMock.Object,
        _roleRepoMock.Object,
        _projectRepoMock.Object);

    [Fact]
    public async Task ExecuteAsync_WhenStandardUserWithProjects_ReturnsProfileAndAccessibleProjects()
    {
        // Arrange
        var now = TestConstants.BaselineTime;
        var user = UserBuilder.AUser()
            .WithEmail("user@example.com")
            .WithName("John", "Doe")
            .WithTime(now)
            .Build();

        var projectId = Guid.NewGuid();
        var project = ProjectTestFactory.Create(projectId, Guid.NewGuid(), "Alpha", "alpha", now);
        var role = UserProjectRoleBuilder.ARole()
            .ForUser(user.Id)
            .InProject(projectId)
            .AsAdmin()
            .WithTime(now)
            .Build();

        _userRepoMock.Setup(r => r.GetByIdAsync(user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        _roleRepoMock.Setup(r => r.ListByUserAsync(user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<UserProjectRole> { role });
        _projectRepoMock.Setup(r => r.ListByIdsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Project> { project });

        // Act
        var result = await CreateSut().ExecuteAsync(new GetCurrentUserProfileQuery(user.Id));

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be(user.Id);
        result.Email.Should().Be("user@example.com");
        result.FirstName.Should().Be("John");
        result.LastName.Should().Be("Doe");
        result.AccessibleProjects.Should().HaveCount(1);
        result.AccessibleProjects[0].Id.Should().Be(projectId);
        result.AccessibleProjects[0].Role.Should().Be("Admin");
        result.DefaultProjectId.Should().Be(projectId);
    }

    [Fact]
    public async Task ExecuteAsync_WhenSuperAdmin_ReturnsAllActiveProjectsWithAdminRole()
    {
        // Arrange
        var now = TestConstants.BaselineTime;
        var user = UserBuilder.AUser()
            .WithEmail("admin@example.com")
            .WithSystemRole(SystemRole.SuperAdmin)
            .WithTime(now)
            .Build();

        var p1 = ProjectTestFactory.Create(Guid.NewGuid(), Guid.NewGuid(), "P1", "p-1", now);
        var p2 = ProjectTestFactory.Create(Guid.NewGuid(), Guid.NewGuid(), "P2", "p-2", now);

        _userRepoMock.Setup(r => r.GetByIdAsync(user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        _projectRepoMock.Setup(r => r.ListActiveAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Project> { p1, p2 });

        // Act
        var result = await CreateSut().ExecuteAsync(new GetCurrentUserProfileQuery(user.Id));

        // Assert
        result.Should().NotBeNull();
        result.SystemRole.Should().Be("SuperAdmin");
        result.AccessibleProjects.Should().HaveCount(2);
        result.AccessibleProjects.All(p => p.Role == "Admin").Should().BeTrue();
    }

    [Fact]
    public async Task ExecuteAsync_WhenUserNotFound_ThrowsUnauthorizedException()
    {
        // Arrange
        _userRepoMock.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        // Act
        var act = () => CreateSut().ExecuteAsync(new GetCurrentUserProfileQuery(Guid.NewGuid()));

        // Assert
        await act.Should().ThrowAsync<UnauthorizedException>();
    }
}
