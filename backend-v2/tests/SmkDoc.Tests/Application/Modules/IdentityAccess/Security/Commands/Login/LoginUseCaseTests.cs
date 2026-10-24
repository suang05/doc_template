using FluentValidation;
using FluentValidation.Results;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using SmkDoc.Application.Common.Constants;
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
using ValidationException = SmkDoc.Application.Common.Exceptions.ValidationException;

namespace SmkDoc.Tests.Application.Modules.IdentityAccess.Security.Commands.Login;

public sealed class LoginUseCaseTests
{
    private readonly IUserRepository _userRepo = Substitute.For<IUserRepository>();
    private readonly IPasswordHasher _passwordHasher = Substitute.For<IPasswordHasher>();
    private readonly IJwtTokenGenerator _jwtGenerator = Substitute.For<IJwtTokenGenerator>();
    private readonly IUserWorkspaceQueryService _workspaceQueryService = Substitute.For<IUserWorkspaceQueryService>();
    private readonly IRefreshTokenRepository _tokenRepo = Substitute.For<IRefreshTokenRepository>();
    private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();
    private readonly IExecutionContext _executionContext = Substitute.For<IExecutionContext>();
    private readonly FakeTimeProvider _timeProvider = TestConstants.CreateFakeClock();
    private readonly ISecurityAuditService _auditService = Substitute.For<ISecurityAuditService>();
    private readonly LoginCommandValidator _defaultValidator = new();

    public LoginUseCaseTests()
    {
        _uow.CommitAsync(Arg.Any<CancellationToken>()).Returns(1);
    }

    private LoginUseCase CreateSut(
        IValidator<LoginCommand>? validator = null,
        Guid? executionContextProjectId = null)
    {
        _executionContext.ProjectId.Returns(executionContextProjectId);

        return new LoginUseCase(
            _userRepo,
            _passwordHasher,
            _jwtGenerator,
            _workspaceQueryService,
            validator ?? _defaultValidator,
            _tokenRepo,
            _uow,
            _timeProvider,
            _executionContext,
            _auditService);
    }

    [Fact]
    public async Task ExecuteAsync_WhenValidCredentialsAndRole_ReturnsTokenAndProjects()
    {
        // Arrange
        var now = TestConstants.BaselineTime;
        var projectId = Guid.NewGuid();
        var command = new LoginCommand("test@example.com", "password123");
        User user = aUser.Active()
            .WithEmail("test@example.com")
            .WithPasswordHash("hashed_pw")
            .WithName("Test", "User")
            .WithSystemRole(SystemRole.Member)
            .WithTime(now);

        var accessibleProjects = new List<AccessibleProjectDto>
        {
            new(projectId, "Project Alpha", "project-alpha", "Viewer")
        };

        var workspaceContext = new UserWorkspaceContextDto(projectId, "Viewer", projectId, accessibleProjects);

        _userRepo.GetByEmailAsync(Arg.Is<EmailAddress>(e => e.Value == "test@example.com"), Arg.Any<CancellationToken>())
            .Returns(user);

        _workspaceQueryService.ResolveWorkspaceContextAsync(user, projectId, true, Arg.Any<CancellationToken>())
            .Returns(workspaceContext);

        _passwordHasher.VerifyPassword("password123", "hashed_pw")
            .Returns(true);

        _jwtGenerator.GenerateToken(user, projectId, Arg.Any<IEnumerable<string>>())
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
        _auditService.Received(1).LogLoginSuccess(user.Id.Value, "test@example.com", projectId);
    }

    [Fact]
    public async Task ExecuteAsync_WhenUserNotFound_ThrowsUnauthorizedException()
    {
        // Arrange
        var command = new LoginCommand("notfound@example.com", "password123");
        _userRepo.GetByEmailAsync(Arg.Any<EmailAddress>(), Arg.Any<CancellationToken>())
            .Returns((User?)null);

        // Act
        var act = () => CreateSut().ExecuteAsync(command);

        // Assert
        await act.Should().ThrowAsync<UnauthorizedException>()
            .WithMessage("Invalid email or password.");
        _passwordHasher.Received(1).VerifyPassword("password123", AuthConstants.DummyPasswordHash);
        _auditService.Received(1).LogLoginFailure("notfound@example.com", "UserNotFound");
    }

    [Fact]
    public async Task ExecuteAsync_WhenUserInactive_ThrowsUnauthorizedException()
    {
        // Arrange
        var now = TestConstants.BaselineTime;
        var command = new LoginCommand("inactive@example.com", "password123");
        User user = aUser.Inactive()
            .WithEmail("inactive@example.com")
            .WithPasswordHash("hashed_pw")
            .WithName("Inactive", "User")
            .WithSystemRole(SystemRole.Member)
            .WithTime(now);

        _userRepo.GetByEmailAsync(Arg.Any<EmailAddress>(), Arg.Any<CancellationToken>())
            .Returns(user);

        // Act
        var act = () => CreateSut().ExecuteAsync(command);

        // Assert
        await act.Should().ThrowAsync<UnauthorizedException>()
            .WithMessage("Invalid email or password.");
        _passwordHasher.Received(1).VerifyPassword("password123", AuthConstants.DummyPasswordHash);
        _auditService.Received(1).LogLoginFailure("inactive@example.com", "UserInactive");
    }

    [Fact]
    public async Task ExecuteAsync_WhenPasswordInvalid_ThrowsUnauthorizedException()
    {
        // Arrange
        var command = new LoginCommand("test@example.com", "wrongpassword");
        User user = aUser.Active()
            .WithEmail("test@example.com")
            .WithPasswordHash("hashed_pw")
            .WithName("Test", "User")
            .WithSystemRole(SystemRole.Member)
            .WithTime(TestConstants.BaselineTime);

        _userRepo.GetByEmailAsync(Arg.Any<EmailAddress>(), Arg.Any<CancellationToken>())
            .Returns(user);
        _passwordHasher.VerifyPassword("wrongpassword", "hashed_pw")
            .Returns(false);

        // Act
        var act = () => CreateSut().ExecuteAsync(command);

        // Assert
        await act.Should().ThrowAsync<UnauthorizedException>()
            .WithMessage("Invalid email or password.");
        _auditService.Received(1).LogLoginFailure("test@example.com", "InvalidPassword");
    }

    [Fact]
    public async Task ExecuteAsync_WhenUserHasNoAccessToSelectedProject_ThrowsUnauthorizedException()
    {
        // Arrange
        var now = TestConstants.BaselineTime;
        var selectedProjectId = Guid.NewGuid();
        var command = new LoginCommand("test@example.com", "password123");
        User user = aUser.Active()
            .WithEmail("test@example.com")
            .WithPasswordHash("hashed_pw")
            .WithName("Test", "User")
            .WithSystemRole(SystemRole.Member)
            .WithTime(now);

        _userRepo.GetByEmailAsync(Arg.Any<EmailAddress>(), Arg.Any<CancellationToken>())
            .Returns(user);
        _passwordHasher.VerifyPassword("password123", "hashed_pw")
            .Returns(true);
        _workspaceQueryService.ResolveWorkspaceContextAsync(user, selectedProjectId, true, Arg.Any<CancellationToken>())
            .Returns<UserWorkspaceContextDto>(_ => throw new UnauthorizedException("User does not have access to the specified project."));

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
        await _userRepo.DidNotReceive().GetByEmailAsync(Arg.Any<EmailAddress>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenEmailMalformedWithoutValidator_ThrowsDomainValidationException()
    {
        // Arrange
        var command = new LoginCommand("invalid-email-format", "password123");
        var passValidator = Substitute.For<IValidator<LoginCommand>>();
        passValidator.ValidateAsync(Arg.Any<LoginCommand>(), Arg.Any<CancellationToken>())
            .Returns(new ValidationResult());

        // Act
        var act = () => CreateSut(validator: passValidator).ExecuteAsync(command);

        // Assert
        await act.Should().ThrowAsync<DomainValidationException>();
        await _userRepo.DidNotReceive().GetByEmailAsync(Arg.Any<EmailAddress>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenTokenRepoProvided_GeneratesRefreshTokenAndCommits()
    {
        // Arrange
        var now = TestConstants.BaselineTime;
        var command = new LoginCommand("test@example.com", "password123");
        User user = aUser.Active()
            .WithEmail("test@example.com")
            .WithPasswordHash("hashed_pw")
            .WithTime(now);

        var workspaceContext = new UserWorkspaceContextDto(null, "Viewer", null, new List<AccessibleProjectDto>());

        _userRepo.GetByEmailAsync(Arg.Any<EmailAddress>(), Arg.Any<CancellationToken>())
            .Returns(user);
        _workspaceQueryService.ResolveWorkspaceContextAsync(user, Arg.Any<Guid?>(), true, Arg.Any<CancellationToken>())
            .Returns(workspaceContext);
        _passwordHasher.VerifyPassword("password123", "hashed_pw")
            .Returns(true);
        _jwtGenerator.GenerateToken(user, Arg.Any<Guid?>(), Arg.Any<IEnumerable<string>>())
            .Returns("valid_token");

        // Act
        var result = await CreateSut().ExecuteAsync(command);

        // Assert
        result.RefreshToken.Should().NotBeNullOrWhiteSpace();
        await _tokenRepo.Received(1).AddAsync(Arg.Is<RefreshTokenEntity>(t => t.UserId == user.Id), Arg.Any<CancellationToken>());
        await _uow.Received(1).CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenProjectSelected_PopulatesActiveApiKeysFromQueryService()
    {
        // Arrange
        var now = TestConstants.BaselineTime;
        var projectId = Guid.NewGuid();
        var command = new LoginCommand("test@example.com", "password123");
        User user = aUser.Active()
            .WithEmail("test@example.com")
            .WithPasswordHash("hashed_pw")
            .WithSystemRole(SystemRole.SuperAdmin)
            .WithTime(now);

        var accessibleProjects = new List<AccessibleProjectDto>
        {
            new(projectId, "Project Alpha", "project-alpha", "Admin")
        };

        var activeKeys = new List<ApiKeyDto>
        {
            new(Guid.NewGuid(), "Read Key", "test-app", true, null, now, "ReadOnly")
        };

        var workspaceContext = new UserWorkspaceContextDto(projectId, "Admin", projectId, accessibleProjects);

        _userRepo.GetByEmailAsync(Arg.Any<EmailAddress>(), Arg.Any<CancellationToken>())
            .Returns(user);
        _workspaceQueryService.ResolveWorkspaceContextAsync(user, projectId, true, Arg.Any<CancellationToken>())
            .Returns(workspaceContext);
        _workspaceQueryService.GetActiveApiKeysAsync(projectId, Arg.Any<CancellationToken>())
            .Returns(activeKeys);
        _passwordHasher.VerifyPassword("password123", "hashed_pw")
            .Returns(true);
        _jwtGenerator.GenerateToken(user, projectId, Arg.Any<IEnumerable<string>>())
            .Returns("valid_token");

        // Act
        var result = await CreateSut(executionContextProjectId: projectId).ExecuteAsync(command);

        // Assert
        result.ActiveApiKeys.Should().HaveCount(1);
        result.ActiveApiKeys[0].Name.Should().Be("Read Key");
        result.ActiveApiKeys[0].Scope.Should().Be("ReadOnly");
    }
}
