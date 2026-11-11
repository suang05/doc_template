using FluentAssertions;
using NSubstitute;
using SmkDoc.Application.Common.Constants;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.IdentityAccess.Security.Commands.Login;
using SmkDoc.Application.Modules.IdentityAccess.Security.DTOs;
using SmkDoc.Application.Modules.IdentityAccess.Security.Services;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Enums;
using SmkDoc.Domain.Exceptions;
using SmkDoc.Domain.Interfaces;
using SmkDoc.Domain.ValueObjects;
using SmkDoc.Tests.Common.Builders;
using Xunit;

namespace SmkDoc.Tests.Application.Modules.IdentityAccess.Security.Commands.Login;

public sealed class LoginUseCaseTests
{
    private readonly IUserRepository _userRepo = Substitute.For<IUserRepository>();
    private readonly IPasswordHasher _passwordHasher = Substitute.For<IPasswordHasher>();
    private readonly ITokenIssuerService _tokenIssuerService = Substitute.For<ITokenIssuerService>();
    private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();
    private readonly ISecurityAuditService _auditService = Substitute.For<ISecurityAuditService>();

    public LoginUseCaseTests()
    {
        _uow.CommitAsync(Arg.Any<CancellationToken>()).Returns(1);
    }

    private LoginUseCase CreateSut() =>
        new(_userRepo, _passwordHasher, _tokenIssuerService, _uow, _auditService);

    [Fact]
    public async Task ExecuteAsync_WhenValidCredentials_IssuesTokensAndCommitsSession()
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

        var accessibleProjects = new List<AccessibleProjectResultDto>
        {
            new(projectId, "Project Alpha", "project-alpha", "Viewer")
        };
        var workspaceContext = new UserWorkspaceContextResultDto(projectId, "Viewer", projectId, accessibleProjects);
        var issuedTokens = new IssuedTokensResult(
            "valid_token",
            "raw_refresh_token",
            workspaceContext,
            []);

        _userRepo.GetByEmailAsync(Arg.Is<EmailAddress>(e => e.Value == "test@example.com"), Arg.Any<CancellationToken>())
            .Returns(user);

        _passwordHasher.VerifyPassword("password123", "hashed_pw")
            .Returns(true);

        _tokenIssuerService.IssueTokensAsync(user, Arg.Any<Guid?>(), Arg.Any<CancellationToken>())
            .Returns(issuedTokens);

        // Act
        var result = await CreateSut().ExecuteAsync(command);

        // Assert
        result.Should().NotBeNull();
        result.AccessToken.Should().Be("valid_token");
        result.RefreshToken.Should().Be("raw_refresh_token");
        result.User.Email.Should().Be("test@example.com");
        result.AccessibleProjects.Should().HaveCount(1);
        result.ActiveProjectId.Should().Be(projectId);
        result.ActiveRole.Should().Be("Viewer");

        await _uow.Received(1).CommitAsync(Arg.Any<CancellationToken>());
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
        await _uow.DidNotReceive().CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenUserInactive_ThrowsUnauthorizedException()
    {
        // Arrange
        var command = new LoginCommand("inactive@example.com", "password123");
        User inactiveUser = aUser.Inactive().WithEmail("inactive@example.com");

        _userRepo.GetByEmailAsync(Arg.Any<EmailAddress>(), Arg.Any<CancellationToken>())
            .Returns(inactiveUser);

        // Act
        var act = () => CreateSut().ExecuteAsync(command);

        // Assert
        await act.Should().ThrowAsync<UnauthorizedException>()
            .WithMessage("Invalid email or password.");
        _passwordHasher.Received(1).VerifyPassword("password123", AuthConstants.DummyPasswordHash);
        _auditService.Received(1).LogLoginFailure("inactive@example.com", "UserInactive");
        await _uow.DidNotReceive().CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenInvalidPassword_ThrowsUnauthorizedException()
    {
        // Arrange
        var command = new LoginCommand("test@example.com", "wrongpassword");
        User user = aUser.Active()
            .WithEmail("test@example.com")
            .WithPasswordHash("hashed_pw");

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
        await _uow.DidNotReceive().CommitAsync(Arg.Any<CancellationToken>());
    }
}
