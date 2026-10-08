using FluentValidation;
using Microsoft.Extensions.Time.Testing;
using ValidationException = SmkDoc.Application.Common.Exceptions.ValidationException;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.IdentityAccess.Security.Commands.Login;
using SmkDoc.Application.Modules.IdentityAccess.Security.DTOs;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Enums;
using SmkDoc.Domain.Exceptions;
using SmkDoc.Domain.Interfaces;
using SmkDoc.Domain.ValueObjects;
using SmkDoc.Tests.Common.Builders;
using RefreshTokenEntity = SmkDoc.Domain.Entities.RefreshToken;

namespace SmkDoc.Tests.Application.Modules.IdentityAccess.Security.Commands.Login;

public class LoginUseCaseTests
{
    private readonly Mock<IUserRepository> _userRepoMock = new();
    private readonly Mock<IPasswordHasher> _passwordHasherMock = new();
    private readonly Mock<IJwtTokenGenerator> _jwtGeneratorMock = new();
    private readonly Mock<IUserWorkspaceQueryService> _workspaceQueryServiceMock = new();
    private readonly Mock<IRefreshTokenRepository> _tokenRepoMock = new();
    private readonly Mock<IUnitOfWork> _uowMock = new();
    private readonly Mock<IExecutionContext> _executionContextMock = new();
    private readonly FakeTimeProvider _timeProvider = new(TestConstants.BaselineTime);

    private LoginUseCase CreateSut(
        IValidator<LoginCommand>? validator = null,
        bool withTokenRepo = false,
        Guid? executionContextProjectId = null)
    {
        _executionContextMock.Setup(x => x.ProjectId).Returns(executionContextProjectId);

        return new(
            _userRepoMock.Object,
            _passwordHasherMock.Object,
            _jwtGeneratorMock.Object,
            _workspaceQueryServiceMock.Object,
            validator,
            withTokenRepo ? _tokenRepoMock.Object : null,
            withTokenRepo ? _uowMock.Object : null,
            _timeProvider,
            _executionContextMock.Object);
    }

    [Fact]
    public async Task ExecuteAsync_WhenValidCredentialsAndRole_ReturnsTokenAndProjects()
    {
        // Arrange
        var now = TestConstants.BaselineTime;
        var projectId = Guid.NewGuid();
        var command = new LoginCommand("test@example.com", "password123");
        var user = UserBuilder.AUser()
            .WithEmail("test@example.com")
            .WithPasswordHash("hashed_pw")
            .WithName("Test", "User")
            .WithSystemRole(SystemRole.Member)
            .WithTime(now)
            .Build();

        var accessibleProjects = new List<AccessibleProjectDto>
        {
            new(projectId, "Project Alpha", "project-alpha", "Viewer")
        };

        _userRepoMock.Setup(r => r.GetByEmailAsync(It.Is<EmailAddress>(e => e.Value == "test@example.com"), It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        _workspaceQueryServiceMock.Setup(q => q.GetAccessibleProjectsAsync(user.Id, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(accessibleProjects);

        _passwordHasherMock.Setup(p => p.VerifyPassword("password123", "hashed_pw"))
            .Returns(true);

        _jwtGeneratorMock.Setup(j => j.GenerateToken(user, projectId, It.IsAny<IEnumerable<string>>()))
            .Returns("valid_token");

        // Act
        var result = await CreateSut(executionContextProjectId: projectId).ExecuteAsync(command);

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
        _userRepoMock.Setup(r => r.GetByEmailAsync(It.IsAny<EmailAddress>(), It.IsAny<CancellationToken>()))
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

        _userRepoMock.Setup(r => r.GetByEmailAsync(It.IsAny<EmailAddress>(), It.IsAny<CancellationToken>()))
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

        _userRepoMock.Setup(r => r.GetByEmailAsync(It.IsAny<EmailAddress>(), It.IsAny<CancellationToken>()))
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
        var command = new LoginCommand("test@example.com", "password123");
        var user = UserBuilder.AUser()
            .WithEmail("test@example.com")
            .WithPasswordHash("hashed_pw")
            .WithName("Test", "User")
            .WithSystemRole(SystemRole.Member)
            .WithTime(now)
            .Build();

        var accessibleProjects = new List<AccessibleProjectDto>
        {
            new(anotherProjectId, "Another", "another", "Viewer")
        };

        _userRepoMock.Setup(r => r.GetByEmailAsync(It.IsAny<EmailAddress>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        _passwordHasherMock.Setup(p => p.VerifyPassword("password123", "hashed_pw"))
            .Returns(true);
        _workspaceQueryServiceMock.Setup(q => q.GetAccessibleProjectsAsync(user.Id, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(accessibleProjects);

        // Act
        var act = () => CreateSut(executionContextProjectId: selectedProjectId).ExecuteAsync(command);

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
        _userRepoMock.Verify(r => r.GetByEmailAsync(It.IsAny<EmailAddress>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_WhenEmailMalformedWithoutValidator_ThrowsDomainValidationException()
    {
        // Arrange
        var command = new LoginCommand("invalid-email-format", "password123");

        // Act
        var act = () => CreateSut().ExecuteAsync(command);

        // Assert
        await act.Should().ThrowAsync<DomainValidationException>();
        _userRepoMock.Verify(r => r.GetByEmailAsync(It.IsAny<EmailAddress>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_WhenTokenRepoProvided_GeneratesRefreshTokenAndCommits()
    {
        // Arrange
        var now = TestConstants.BaselineTime;
        var command = new LoginCommand("test@example.com", "password123");
        var user = UserBuilder.AUser()
            .WithEmail("test@example.com")
            .WithPasswordHash("hashed_pw")
            .WithTime(now)
            .Build();

        _userRepoMock.Setup(r => r.GetByEmailAsync(It.IsAny<EmailAddress>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        _workspaceQueryServiceMock.Setup(q => q.GetAccessibleProjectsAsync(user.Id, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<AccessibleProjectDto>());
        _passwordHasherMock.Setup(p => p.VerifyPassword("password123", "hashed_pw"))
            .Returns(true);
        _jwtGeneratorMock.Setup(j => j.GenerateToken(user, It.IsAny<Guid?>(), It.IsAny<IEnumerable<string>>()))
            .Returns("valid_token");

        // Act
        var result = await CreateSut(withTokenRepo: true).ExecuteAsync(command);

        // Assert
        result.RefreshToken.Should().NotBeNullOrWhiteSpace();
        _tokenRepoMock.Verify(r => r.AddAsync(It.Is<RefreshTokenEntity>(t => t.UserId == user.Id), It.IsAny<CancellationToken>()), Times.Once);
        _uowMock.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_WhenProjectSelected_PopulatesActiveApiKeysFromQueryService()
    {
        // Arrange
        var now = TestConstants.BaselineTime;
        var projectId = Guid.NewGuid();
        var command = new LoginCommand("test@example.com", "password123");
        var user = UserBuilder.AUser()
            .WithEmail("test@example.com")
            .WithPasswordHash("hashed_pw")
            .WithSystemRole(SystemRole.SuperAdmin)
            .WithTime(now)
            .Build();

        var accessibleProjects = new List<AccessibleProjectDto>
        {
            new(projectId, "Project Alpha", "project-alpha", "Admin")
        };

        var activeKeys = new List<ApiKeyDto>
        {
            new(Guid.NewGuid(), "Read Key", "test-app", true, null, now, "ReadOnly")
        };

        _userRepoMock.Setup(r => r.GetByEmailAsync(It.IsAny<EmailAddress>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        _workspaceQueryServiceMock.Setup(q => q.GetAccessibleProjectsAsync(user.Id, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(accessibleProjects);
        _workspaceQueryServiceMock.Setup(q => q.GetActiveApiKeysAsync(projectId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(activeKeys);
        _passwordHasherMock.Setup(p => p.VerifyPassword("password123", "hashed_pw"))
            .Returns(true);
        _jwtGeneratorMock.Setup(j => j.GenerateToken(user, projectId, It.IsAny<IEnumerable<string>>()))
            .Returns("valid_token");

        // Act
        var result = await CreateSut(executionContextProjectId: projectId).ExecuteAsync(command);

        // Assert
        result.ActiveApiKeys.Should().HaveCount(1);
        result.ActiveApiKeys[0].Name.Should().Be("Read Key");
        result.ActiveApiKeys[0].Scope.Should().Be("ReadOnly");
    }
}
