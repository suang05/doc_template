using FluentValidation;
using Microsoft.Extensions.Time.Testing;
using ValidationException = SmkDoc.Application.Common.Exceptions.ValidationException;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.IdentityAccess.Security.Commands.Login;
using SmkDoc.Application.Modules.IdentityAccess.Security.DTOs;
using SmkDoc.Domain.Constants;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Enums;
using SmkDoc.Domain.Exceptions;
using SmkDoc.Domain.Interfaces;
using SmkDoc.Domain.ValueObjects;
using SmkDoc.Tests.Common.Builders;
using SmkDoc.Tests.Common.Fixtures;
using RefreshTokenEntity = SmkDoc.Domain.Entities.RefreshToken;

namespace SmkDoc.Tests.Application.Modules.IdentityAccess.Security.Commands.Login;

public class LoginUseCaseTests
{
    private readonly SecurityModuleTestFixture _fixture = new();

    private LoginUseCase CreateSut(
        IValidator<LoginCommand>? validator = null,
        Guid? executionContextProjectId = null) =>
        _fixture.BuildLoginUseCase(validator, executionContextProjectId);

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

        var workspaceContext = new UserWorkspaceContextDto(projectId, "Viewer", projectId, accessibleProjects);

        _fixture.UserRepo.Setup(r => r.GetByEmailAsync(It.Is<EmailAddress>(e => e.Value == "test@example.com"), It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        _fixture.WorkspaceQueryService.Setup(q => q.ResolveWorkspaceContextAsync(user, projectId, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(workspaceContext);

        _fixture.PasswordHasher.Setup(p => p.VerifyPassword("password123", "hashed_pw"))
            .Returns(true);

        _fixture.JwtGenerator.Setup(j => j.GenerateToken(user, projectId, It.IsAny<IEnumerable<string>>()))
            .Returns("valid_token");

        // Act
        var result = await CreateSut(executionContextProjectId: projectId).ExecuteAsync(command);

        // Assert
        result.Should().NotBeNull();
        result.AccessToken.Should().Be("valid_token");
        result.User.Email.Should().Be("test@example.com");
        result.AccessibleProjects.Should().HaveCount(1);
        result.AccessibleProjects[0].Name.Should().Be("Project Alpha");
        result.ActiveProjectId.Should().Be(projectId);
        result.ActiveRole.Should().Be("Viewer");
        _fixture.AuditService.Verify(a => a.LogLoginSuccess(user.Id, "test@example.com", projectId), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_WhenUserNotFound_ThrowsUnauthorizedException()
    {
        // Arrange
        var command = new LoginCommand("notfound@example.com", "password123");
        _fixture.UserRepo.Setup(r => r.GetByEmailAsync(It.IsAny<EmailAddress>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        // Act
        var act = () => CreateSut().ExecuteAsync(command);

        // Assert
        await act.Should().ThrowAsync<UnauthorizedException>()
            .WithMessage("Invalid email or password.");
        _fixture.PasswordHasher.Verify(p => p.VerifyPassword("password123", SecurityConstants.DummyPasswordHash), Times.Once);
        _fixture.AuditService.Verify(a => a.LogLoginFailure("notfound@example.com", "UserNotFound"), Times.Once);
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

        _fixture.UserRepo.Setup(r => r.GetByEmailAsync(It.IsAny<EmailAddress>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        // Act
        var act = () => CreateSut().ExecuteAsync(command);

        // Assert
        await act.Should().ThrowAsync<UnauthorizedException>()
            .WithMessage("Invalid email or password.");
        _fixture.PasswordHasher.Verify(p => p.VerifyPassword("password123", SecurityConstants.DummyPasswordHash), Times.Once);
        _fixture.AuditService.Verify(a => a.LogLoginFailure("inactive@example.com", "UserInactive"), Times.Once);
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

        _fixture.UserRepo.Setup(r => r.GetByEmailAsync(It.IsAny<EmailAddress>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        _fixture.PasswordHasher.Setup(p => p.VerifyPassword("wrongpassword", "hashed_pw"))
            .Returns(false);

        // Act
        var act = () => CreateSut().ExecuteAsync(command);

        // Assert
        await act.Should().ThrowAsync<UnauthorizedException>()
            .WithMessage("Invalid email or password.");
        _fixture.AuditService.Verify(a => a.LogLoginFailure("test@example.com", "InvalidPassword"), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_WhenUserHasNoAccessToSelectedProject_ThrowsUnauthorizedException()
    {
        // Arrange
        var now = TestConstants.BaselineTime;
        var selectedProjectId = Guid.NewGuid();
        var command = new LoginCommand("test@example.com", "password123");
        var user = UserBuilder.AUser()
            .WithEmail("test@example.com")
            .WithPasswordHash("hashed_pw")
            .WithName("Test", "User")
            .WithSystemRole(SystemRole.Member)
            .WithTime(now)
            .Build();

        _fixture.UserRepo.Setup(r => r.GetByEmailAsync(It.IsAny<EmailAddress>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        _fixture.PasswordHasher.Setup(p => p.VerifyPassword("password123", "hashed_pw"))
            .Returns(true);
        _fixture.WorkspaceQueryService.Setup(q => q.ResolveWorkspaceContextAsync(user, selectedProjectId, true, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new UnauthorizedException("User does not have access to the specified project."));

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
        _fixture.UserRepo.Verify(r => r.GetByEmailAsync(It.IsAny<EmailAddress>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_WhenEmailMalformedWithoutValidator_ThrowsDomainValidationException()
    {
        // Arrange
        var command = new LoginCommand("invalid-email-format", "password123");

        // Act
        var passValidator = new Mock<IValidator<LoginCommand>>();
        passValidator.Setup(v => v.ValidateAsync(It.IsAny<LoginCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FluentValidation.Results.ValidationResult());

        var act = () => CreateSut(validator: passValidator.Object).ExecuteAsync(command);

        // Assert
        await act.Should().ThrowAsync<DomainValidationException>();
        _fixture.UserRepo.Verify(r => r.GetByEmailAsync(It.IsAny<EmailAddress>(), It.IsAny<CancellationToken>()), Times.Never);
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

        var workspaceContext = new UserWorkspaceContextDto(null, "Viewer", null, new List<AccessibleProjectDto>());

        _fixture.UserRepo.Setup(r => r.GetByEmailAsync(It.IsAny<EmailAddress>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        _fixture.WorkspaceQueryService.Setup(q => q.ResolveWorkspaceContextAsync(user, It.IsAny<Guid?>(), true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(workspaceContext);
        _fixture.PasswordHasher.Setup(p => p.VerifyPassword("password123", "hashed_pw"))
            .Returns(true);
        _fixture.JwtGenerator.Setup(j => j.GenerateToken(user, It.IsAny<Guid?>(), It.IsAny<IEnumerable<string>>()))
            .Returns("valid_token");

        // Act
        var result = await CreateSut().ExecuteAsync(command);

        // Assert
        result.RefreshToken.Should().NotBeNullOrWhiteSpace();
        _fixture.TokenRepo.Verify(r => r.AddAsync(It.Is<RefreshTokenEntity>(t => t.UserId == user.Id), It.IsAny<CancellationToken>()), Times.Once);
        _fixture.Uow.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
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

        var workspaceContext = new UserWorkspaceContextDto(projectId, "Admin", projectId, accessibleProjects);

        _fixture.UserRepo.Setup(r => r.GetByEmailAsync(It.IsAny<EmailAddress>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        _fixture.WorkspaceQueryService.Setup(q => q.ResolveWorkspaceContextAsync(user, projectId, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(workspaceContext);
        _fixture.WorkspaceQueryService.Setup(q => q.GetActiveApiKeysAsync(projectId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(activeKeys);
        _fixture.PasswordHasher.Setup(p => p.VerifyPassword("password123", "hashed_pw"))
            .Returns(true);
        _fixture.JwtGenerator.Setup(j => j.GenerateToken(user, projectId, It.IsAny<IEnumerable<string>>()))
            .Returns("valid_token");

        // Act
        var result = await CreateSut(executionContextProjectId: projectId).ExecuteAsync(command);

        // Assert
        result.ActiveApiKeys.Should().HaveCount(1);
        result.ActiveApiKeys[0].Name.Should().Be("Read Key");
        result.ActiveApiKeys[0].Scope.Should().Be("ReadOnly");
    }
}
