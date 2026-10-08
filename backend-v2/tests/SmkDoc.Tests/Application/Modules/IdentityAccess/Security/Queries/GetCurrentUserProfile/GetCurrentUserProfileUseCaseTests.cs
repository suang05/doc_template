using FluentAssertions;
using Moq;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.IdentityAccess.Security.DTOs;
using SmkDoc.Application.Modules.IdentityAccess.Security.Queries.GetCurrentUserProfile;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Enums;
using SmkDoc.Domain.Exceptions;
using SmkDoc.Domain.Interfaces;
using SmkDoc.Tests.Common.Builders;
using Xunit;

namespace SmkDoc.Tests.Application.Modules.IdentityAccess.Security.Queries.GetCurrentUserProfile;

public class GetCurrentUserProfileUseCaseTests
{
    private readonly Mock<IUserRepository> _userRepoMock = new();
    private readonly Mock<IUserWorkspaceQueryService> _workspaceQueryServiceMock = new();

    private GetCurrentUserProfileUseCase CreateSut() => new(
        _userRepoMock.Object,
        _workspaceQueryServiceMock.Object);

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
        var projects = new List<AccessibleProjectDto>
        {
            new(projectId, "Alpha", "alpha", "Admin")
        };

        _userRepoMock.Setup(r => r.GetByIdAsync(user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        _workspaceQueryServiceMock.Setup(q => q.GetAccessibleProjectsAsync(user.Id, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(projects);

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

        var projects = new List<AccessibleProjectDto>
        {
            new(Guid.NewGuid(), "P1", "p-1", "Admin"),
            new(Guid.NewGuid(), "P2", "p-2", "Admin")
        };

        _userRepoMock.Setup(r => r.GetByIdAsync(user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        _workspaceQueryServiceMock.Setup(q => q.GetAccessibleProjectsAsync(user.Id, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(projects);

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
