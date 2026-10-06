using FluentValidation;
using ValidationException = SmkDoc.Application.Common.Exceptions.ValidationException;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.IdentityAccess.Security.Commands.Login;
using SmkDoc.Application.Modules.IdentityAccess.Security.DTOs;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Enums;
using SmkDoc.Domain.ValueObjects;
using SmkDoc.Tests.Common.Builders;
using SmkDoc.Tests.Common.Factories;

namespace SmkDoc.Tests.Application.Modules.IdentityAccess.Security.Commands.Login;

public class LoginUseCaseTests
{
    private readonly Mock<IUserRepository> _userRepoMock = new();
    private readonly Mock<IUserProjectRoleRepository> _roleRepoMock = new();
    private readonly Mock<IProjectRepository> _projectRepoMock = new();
    private readonly Mock<IPasswordHasher> _passwordHasherMock = new();
    private readonly Mock<IJwtTokenGenerator> _jwtGeneratorMock = new();
    private LoginUseCase CreateSut(IValidator<LoginCommand>? validator = null) => new(
        _userRepoMock.Object,
        _roleRepoMock.Object,
        _projectRepoMock.Object,
        _passwordHasherMock.Object,
        _jwtGeneratorMock.Object,
        validator);

    [Fact]
    public async Task ExecuteAsync_WhenValidCredentialsAndRole_ReturnsTokenAndProjects()
    {
        // Arrange
        var now = TestConstants.BaselineTime;
        var projectId = Guid.NewGuid();
        var command = new LoginCommand("test@example.com", "password123", projectId);
        var user = UserBuilder.AUser()
            .WithEmail("test@example.com")
            .WithPasswordHash("hashed_pw")
            .WithName("Test", "User")
            .WithSystemRole(SystemRole.Member)
            .WithTime(now)
            .Build();
        var role = UserProjectRoleBuilder.ARole()
            .ForUser(user.Id)
            .InProject(projectId)
            .AsViewer()
            .WithTime(now)
            .Build();
        var project = ProjectTestFactory.Create(projectId, Guid.NewGuid(), "Project Alpha", "project-alpha", now);

        _userRepoMock.Setup(r => r.GetByEmailAsync("test@example.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        _roleRepoMock.Setup(r => r.ListByUserAsync(user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<UserProjectRole> { role });

        _projectRepoMock.Setup(r => r.ListByIdsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Project> { project });

        _passwordHasherMock.Setup(p => p.VerifyPassword("password123", "hashed_pw"))
            .Returns(true);

        _jwtGeneratorMock.Setup(j => j.GenerateToken(user, projectId, It.IsAny<IEnumerable<string>>()))
            .Returns("valid_token");

        // Act
        var result = await CreateSut().ExecuteAsync(command);

        // Assert
        result.Should().NotBeNull();
        result.AccessToken.Should().Be("valid_token");
        result.User.Email.Should().Be("test@example.com");
        result.AccessibleProjects.Should().HaveCount(1);
        result.AccessibleProjects[0].Name.Should().Be("Project Alpha");
    }

    [Fact]
    public async Task ExecuteAsync_WhenUserNotFound_ThrowsUnauthorizedException()
    {
        // Arrange
        var command = new LoginCommand("notfound@example.com", "password123");
        _userRepoMock.Setup(r => r.GetByEmailAsync("notfound@example.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        // Act
        var act = () => CreateSut().ExecuteAsync(command);

        // Assert
        await act.Should().ThrowAsync<UnauthorizedException>()
            .WithMessage("Invalid email or password.");
    }

    [Fact]
    public async Task ExecuteAsync_WhenUserInactive_ThrowsUnauthorizedException()
    {
        // Arrange
        var now = TestConstants.BaselineTime;
        var command = new LoginCommand("inactive@example.com", "password123");
        var user = UserBuilder.AUser()
            .WithEmail("inactive@example.com")
            .WithPasswordHash("hashed_pw")
            .WithName("Inactive", "User")
            .WithSystemRole(SystemRole.Member)
            .AsInactive()
            .WithTime(now)
            .Build();

        _userRepoMock.Setup(r => r.GetByEmailAsync("inactive@example.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        // Act
        var act = () => CreateSut().ExecuteAsync(command);

        // Assert
        await act.Should().ThrowAsync<UnauthorizedException>()
            .WithMessage("Invalid email or password.");
    }

    [Fact]
    public async Task ExecuteAsync_WhenPasswordInvalid_ThrowsUnauthorizedException()
    {
        // Arrange
        var command = new LoginCommand("test@example.com", "wrongpassword");
        var user = UserBuilder.AUser()
            .WithEmail("test@example.com")
            .WithPasswordHash("hashed_pw")
            .WithName("Test", "User")
            .WithSystemRole(SystemRole.Member)
            .WithTime(TestConstants.BaselineTime)
            .Build();

        _userRepoMock.Setup(r => r.GetByEmailAsync("test@example.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        _passwordHasherMock.Setup(p => p.VerifyPassword("wrongpassword", "hashed_pw"))
            .Returns(false);

        // Act
        var act = () => CreateSut().ExecuteAsync(command);

        // Assert
        await act.Should().ThrowAsync<UnauthorizedException>()
            .WithMessage("Invalid email or password.");
    }

    [Fact]
    public async Task ExecuteAsync_WhenUserHasNoAccessToSelectedProject_ThrowsUnauthorizedException()
    {
        // Arrange
        var now = TestConstants.BaselineTime;
        var selectedProjectId = Guid.NewGuid();
        var anotherProjectId = Guid.NewGuid();
        var command = new LoginCommand("test@example.com", "password123", selectedProjectId);
        var user = UserBuilder.AUser()
            .WithEmail("test@example.com")
            .WithPasswordHash("hashed_pw")
            .WithName("Test", "User")
            .WithSystemRole(SystemRole.Member)
            .WithTime(now)
            .Build();
        var role = UserProjectRoleBuilder.ARole()
            .ForUser(user.Id)
            .InProject(anotherProjectId)
            .AsViewer()
            .WithTime(now)
            .Build();
        var project = ProjectBuilder.AProject()
            .WithId(anotherProjectId)
            .WithName("Another")
            .WithSlug("another")
            .WithTime(now)
            .Build();

        _userRepoMock.Setup(r => r.GetByEmailAsync("test@example.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        _passwordHasherMock.Setup(p => p.VerifyPassword("password123", "hashed_pw"))
            .Returns(true);
        _roleRepoMock.Setup(r => r.ListByUserAsync(user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<UserProjectRole> { role });
        _projectRepoMock.Setup(r => r.ListByIdsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Project> { project });

        // Act
        var act = () => CreateSut().ExecuteAsync(command);

        // Assert
        await act.Should().ThrowAsync<UnauthorizedException>()
            .WithMessage("User does not have access to the specified project.");
    }

    [Fact]
    public async Task ExecuteAsync_WhenValidationFails_ThrowsValidationException()
    {
        // Arrange
        var command = new LoginCommand("invalid-email", "123");
        var validator = new LoginCommandValidator();

        // Act
        var act = () => CreateSut(validator).ExecuteAsync(command);

        // Assert
        await act.Should().ThrowAsync<ValidationException>();
        _userRepoMock.Verify(r => r.GetByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
