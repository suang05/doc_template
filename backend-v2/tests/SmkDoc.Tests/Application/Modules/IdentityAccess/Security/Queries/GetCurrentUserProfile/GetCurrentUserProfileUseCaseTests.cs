using FluentAssertions;
using NSubstitute;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.IdentityAccess.Security.DTOs;
using SmkDoc.Application.Modules.IdentityAccess.Security.Queries.GetCurrentUserProfile;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Enums;
using SmkDoc.Domain.Exceptions;
using SmkDoc.Domain.Interfaces;
using SmkDoc.Domain.ValueObjects.Ids;
using SmkDoc.Tests.Common.Builders;
using Xunit;

namespace SmkDoc.Tests.Application.Modules.IdentityAccess.Security.Queries.GetCurrentUserProfile;

public sealed class GetCurrentUserProfileUseCaseTests
{
    private readonly IUserRepository _userRepo = Substitute.For<IUserRepository>();
    private readonly IUserWorkspaceQueryService _workspaceQueryService = Substitute.For<IUserWorkspaceQueryService>();
    private readonly ICurrentProjectContext _projectContext = Substitute.For<ICurrentProjectContext>();

    private GetCurrentUserProfileUseCase CreateSut() => new(
        _userRepo,
        _workspaceQueryService,
        _projectContext);

    [Fact]
    public async Task ExecuteAsync_WhenStandardUserWithProjects_ReturnsProfileAndAccessibleProjects()
    {
        // Arrange
        var now = TestConstants.BaselineTime;
        User user = aUser.Active()
            .WithEmail("user@example.com")
            .WithName("John", "Doe")
            .WithTime(now);

        var projectId = Guid.NewGuid();
        var projects = new List<AccessibleProjectDto>
        {
            new(projectId, "Alpha", "alpha", "Admin")
        };

        var workspaceContext = new UserWorkspaceContextDto(projectId, "Admin", projectId, projects);

        _userRepo.GetByIdAsync(user.Id, Arg.Any<CancellationToken>())
            .Returns(user);
        _workspaceQueryService.ResolveWorkspaceContextAsync(user, Arg.Any<Guid?>(), false, Arg.Any<CancellationToken>())
            .Returns(workspaceContext);

        // Act
        var result = await CreateSut().ExecuteAsync(new GetCurrentUserProfileQuery(user.Id.Value));

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be(user.Id.Value);
        result.Email.Should().Be("user@example.com");
        result.FirstName.Should().Be("John");
        result.LastName.Should().Be("Doe");
        result.AccessibleProjects.Should().HaveCount(1);
        result.AccessibleProjects[0].Id.Should().Be(projectId);
        result.AccessibleProjects[0].Role.Should().Be("Admin");
        result.DefaultProjectId.Should().Be(projectId);
        result.ActiveProjectId.Should().Be(projectId);
    }

    [Fact]
    public async Task ExecuteAsync_WhenActiveProjectIdProvidedAndAccessible_ReturnsActiveProjectId()
    {
        // Arrange
        var now = TestConstants.BaselineTime;
        User user = aUser.Active()
            .WithEmail("user@example.com")
            .WithName("John", "Doe")
            .WithTime(now);

        var proj1 = Guid.NewGuid();
        var proj2 = Guid.NewGuid();
        var projects = new List<AccessibleProjectDto>
        {
            new(proj1, "Project 1", "proj-1", "Member"),
            new(proj2, "Project 2", "proj-2", "Admin")
        };

        var workspaceContext = new UserWorkspaceContextDto(proj2, "Admin", proj1, projects);

        _userRepo.GetByIdAsync(user.Id, Arg.Any<CancellationToken>())
            .Returns(user);
        _projectContext.IsResolved.Returns(true);
        _projectContext.ProjectId.Returns(new ProjectId(proj2));
        _workspaceQueryService.ResolveWorkspaceContextAsync(user, proj2, false, Arg.Any<CancellationToken>())
            .Returns(workspaceContext);

        // Act
        var result = await CreateSut().ExecuteAsync(new GetCurrentUserProfileQuery(user.Id.Value));

        // Assert
        result.Should().NotBeNull();
        result.DefaultProjectId.Should().Be(proj1);
        result.ActiveProjectId.Should().Be(proj2);
    }

    [Fact]
    public async Task ExecuteAsync_WhenActiveProjectIdNotAccessible_FallsBackToDefaultProjectId()
    {
        // Arrange
        var now = TestConstants.BaselineTime;
        User user = aUser.Active()
            .WithEmail("user@example.com")
            .WithName("John", "Doe")
            .WithTime(now);

        var proj1 = Guid.NewGuid();
        var projects = new List<AccessibleProjectDto>
        {
            new(proj1, "Project 1", "proj-1", "Member")
        };

        var unauthorizedProjectId = Guid.NewGuid();

        // Since unauthorizedProjectId is not accessible, workspace context resolves active project to proj1
        var workspaceContext = new UserWorkspaceContextDto(proj1, "Member", proj1, projects);

        _userRepo.GetByIdAsync(user.Id, Arg.Any<CancellationToken>())
            .Returns(user);
        _projectContext.IsResolved.Returns(true);
        _projectContext.ProjectId.Returns(new ProjectId(unauthorizedProjectId));
        _workspaceQueryService.ResolveWorkspaceContextAsync(user, unauthorizedProjectId, false, Arg.Any<CancellationToken>())
            .Returns(workspaceContext);

        // Act
        var result = await CreateSut().ExecuteAsync(new GetCurrentUserProfileQuery(user.Id.Value));

        // Assert
        result.Should().NotBeNull();
        result.DefaultProjectId.Should().Be(proj1);
        result.ActiveProjectId.Should().Be(proj1);
    }

    [Fact]
    public async Task ExecuteAsync_WhenSuperAdmin_ReturnsAllActiveProjectsWithAdminRole()
    {
        // Arrange
        var now = TestConstants.BaselineTime;
        User user = aUser.Active()
            .WithEmail("admin@example.com")
            .WithSystemRole(SystemRole.SuperAdmin)
            .WithTime(now);

        var projects = new List<AccessibleProjectDto>
        {
            new(Guid.NewGuid(), "P1", "p-1", "Admin"),
            new(Guid.NewGuid(), "P2", "p-2", "Admin")
        };

        var workspaceContext = new UserWorkspaceContextDto(projects[0].Id, "Admin", projects[0].Id, projects);

        _userRepo.GetByIdAsync(user.Id, Arg.Any<CancellationToken>())
            .Returns(user);
        _workspaceQueryService.ResolveWorkspaceContextAsync(user, Arg.Any<Guid?>(), false, Arg.Any<CancellationToken>())
            .Returns(workspaceContext);

        // Act
        var result = await CreateSut().ExecuteAsync(new GetCurrentUserProfileQuery(user.Id.Value));

        // Assert
        result.Should().NotBeNull();
        result.SystemRole.Should().Be("SuperAdmin");
        result.AccessibleProjects.Should().HaveCount(2);
        result.AccessibleProjects.All(p => p.Role == "Admin").Should().BeTrue();
        result.DefaultProjectId.Should().Be(projects[0].Id);
        result.ActiveProjectId.Should().Be(projects[0].Id);
    }

    [Fact]
    public async Task ExecuteAsync_WhenSuperAdminWithExplicitProject_GrantsActiveProjectDirectly()
    {
        // Arrange
        var now = TestConstants.BaselineTime;
        User user = aUser.Active()
            .WithEmail("admin@example.com")
            .WithSystemRole(SystemRole.SuperAdmin)
            .WithTime(now);

        var anyProject = Guid.NewGuid();
        var projects = new List<AccessibleProjectDto>
        {
            new(Guid.NewGuid(), "P1", "p-1", "Admin")
        };

        var workspaceContext = new UserWorkspaceContextDto(anyProject, "Admin", projects[0].Id, projects);

        _userRepo.GetByIdAsync(user.Id, Arg.Any<CancellationToken>())
            .Returns(user);
        _projectContext.IsResolved.Returns(true);
        _projectContext.ProjectId.Returns(new ProjectId(anyProject));
        _workspaceQueryService.ResolveWorkspaceContextAsync(user, anyProject, false, Arg.Any<CancellationToken>())
            .Returns(workspaceContext);

        // Act
        var result = await CreateSut().ExecuteAsync(new GetCurrentUserProfileQuery(user.Id.Value));

        // Assert
        result.Should().NotBeNull();
        result.ActiveProjectId.Should().Be(anyProject);
    }

    [Fact]
    public async Task ExecuteAsync_WhenUserNotFound_ThrowsUnauthorizedException()
    {
        // Arrange
        _userRepo.GetByIdAsync(Arg.Any<UserId>(), Arg.Any<CancellationToken>())
            .Returns((User?)null);

        // Act
        var act = () => CreateSut().ExecuteAsync(new GetCurrentUserProfileQuery(Guid.NewGuid()));

        // Assert
        await act.Should().ThrowAsync<UnauthorizedException>();
    }
}
