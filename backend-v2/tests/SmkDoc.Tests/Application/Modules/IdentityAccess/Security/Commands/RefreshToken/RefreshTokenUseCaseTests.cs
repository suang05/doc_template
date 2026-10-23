using FluentAssertions;
using FluentValidation;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.IdentityAccess.Security.Commands.RefreshToken;
using SmkDoc.Application.Modules.IdentityAccess.Security.DTOs;
using SmkDoc.Application.Modules.IdentityAccess.Security.Helpers;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Enums;
using SmkDoc.Domain.Exceptions;
using SmkDoc.Domain.Interfaces;
using SmkDoc.Domain.ValueObjects.Ids;
using SmkDoc.Tests.Common.Builders;
using Xunit;
using RefreshTokenEntity = SmkDoc.Domain.Entities.RefreshToken;

namespace SmkDoc.Tests.Application.Modules.IdentityAccess.Security.Commands.RefreshToken;

public sealed class RefreshTokenUseCaseTests
{
    private readonly IRefreshTokenRepository _tokenRepo = Substitute.For<IRefreshTokenRepository>();
    private readonly IUserRepository _userRepo = Substitute.For<IUserRepository>();
    private readonly IJwtTokenGenerator _jwtGenerator = Substitute.For<IJwtTokenGenerator>();
    private readonly IUserWorkspaceQueryService _workspaceQueryService = Substitute.For<IUserWorkspaceQueryService>();
    private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();
    private readonly RefreshTokenCommandValidator _validator = new();
    private readonly FakeTimeProvider _timeProvider = TestConstants.CreateFakeClock();
    private readonly IExecutionContext _executionContext = Substitute.For<IExecutionContext>();
    private readonly ISecurityAuditService _auditService = Substitute.For<ISecurityAuditService>();

    public RefreshTokenUseCaseTests()
    {
        _uow.CommitAsync(Arg.Any<CancellationToken>()).Returns(1);
    }

    private RefreshTokenUseCase CreateSut(Guid? executionContextProjectId = null)
    {
        _executionContext.ProjectId.Returns(executionContextProjectId);

        return new RefreshTokenUseCase(
            _tokenRepo,
            _userRepo,
            _jwtGenerator,
            _workspaceQueryService,
            _uow,
            _validator,
            _timeProvider,
            _executionContext,
            _auditService);
    }

    [Fact]
    public async Task ExecuteAsync_WhenValidTokenAndNoProjectHeader_RotatesTokenAndDefaultsToFirstAccessibleProject()
    {
        // Arrange
        var now = TestConstants.BaselineTime;
        var rawToken = "my-secret-refresh-token";
        var tokenHash = RefreshTokenHelper.HashToken(rawToken);
        User user = aUser.Active().WithTime(now);
        var existingToken = RefreshTokenEntity.Create(user.Id, tokenHash, now.AddDays(7), now);
        var projectAlphaId = Guid.NewGuid();

        var accessibleProjects = new List<AccessibleProjectDto>
        {
            new(projectAlphaId, "Project Alpha", "alpha", "Admin")
        };

        var workspaceContext = new UserWorkspaceContextDto(projectAlphaId, "Admin", projectAlphaId, accessibleProjects);

        _tokenRepo.GetByHashAsync(tokenHash, Arg.Any<CancellationToken>())
            .Returns(existingToken);
        _userRepo.GetByIdAsync(user.Id, Arg.Any<CancellationToken>())
            .Returns(user);
        _workspaceQueryService.ResolveWorkspaceContextAsync(user, null, true, Arg.Any<CancellationToken>())
            .Returns(workspaceContext);
        _jwtGenerator.GenerateToken(user, projectAlphaId, Arg.Any<IEnumerable<string>>())
            .Returns("new-jwt-token");

        // Act
        var result = await CreateSut().ExecuteAsync(new RefreshTokenCommand(rawToken));

        // Assert
        result.Should().NotBeNull();
        result.AccessToken.Should().Be("new-jwt-token");
        result.RefreshToken.Should().NotBeNullOrWhiteSpace();
        result.RefreshToken.Should().NotBe(rawToken);

        existingToken.IsRevoked.Should().BeTrue();
        await _tokenRepo.Received(1).AddAsync(Arg.Is<RefreshTokenEntity>(t => t.UserId == user.Id), Arg.Any<CancellationToken>());
        await _uow.Received(1).CommitAsync(Arg.Any<CancellationToken>());
        _jwtGenerator.Received(1).GenerateToken(user, projectAlphaId, Arg.Is<IEnumerable<string>>(roles => roles.Contains("Admin")));
        _auditService.Received(1).LogTokenRotated(user.Id.Value);
    }

    [Fact]
    public async Task ExecuteAsync_WhenValidTokenAndProjectHeaderSpecified_RotatesTokenAndPreservesSelectedProjectAndRole()
    {
        // Arrange
        var now = TestConstants.BaselineTime;
        var rawToken = "my-secret-refresh-token";
        var tokenHash = RefreshTokenHelper.HashToken(rawToken);
        User user = aUser.Active().WithTime(now);
        var existingToken = RefreshTokenEntity.Create(user.Id, tokenHash, now.AddDays(7), now);

        var projectAlphaId = Guid.NewGuid();
        var projectBetaId = Guid.NewGuid();

        var accessibleProjects = new List<AccessibleProjectDto>
        {
            new(projectAlphaId, "Project Alpha", "alpha", "Admin"),
            new(projectBetaId, "Project Beta", "beta", "Viewer")
        };

        var workspaceContext = new UserWorkspaceContextDto(projectBetaId, "Viewer", projectAlphaId, accessibleProjects);

        _tokenRepo.GetByHashAsync(tokenHash, Arg.Any<CancellationToken>())
            .Returns(existingToken);
        _userRepo.GetByIdAsync(user.Id, Arg.Any<CancellationToken>())
            .Returns(user);
        _workspaceQueryService.ResolveWorkspaceContextAsync(user, projectBetaId, true, Arg.Any<CancellationToken>())
            .Returns(workspaceContext);
        _jwtGenerator.GenerateToken(user, projectBetaId, Arg.Any<IEnumerable<string>>())
            .Returns("refreshed-beta-jwt");

        // Act
        var result = await CreateSut(executionContextProjectId: projectBetaId).ExecuteAsync(new RefreshTokenCommand(rawToken));

        // Assert
        result.Should().NotBeNull();
        result.AccessToken.Should().Be("refreshed-beta-jwt");
        _jwtGenerator.Received(1).GenerateToken(user, projectBetaId, Arg.Is<IEnumerable<string>>(roles => roles.Contains("Viewer")));
    }

    [Fact]
    public async Task ExecuteAsync_WhenValidTokenAndUnauthorizedProjectHeaderSpecified_ThrowsUnauthorizedException()
    {
        // Arrange
        var now = TestConstants.BaselineTime;
        var rawToken = "my-secret-refresh-token";
        var tokenHash = RefreshTokenHelper.HashToken(rawToken);
        User user = aUser.Active().WithSystemRole(SystemRole.Member).WithTime(now);
        var existingToken = RefreshTokenEntity.Create(user.Id, tokenHash, now.AddDays(7), now);

        var unauthorizedProjectId = Guid.NewGuid();

        _tokenRepo.GetByHashAsync(tokenHash, Arg.Any<CancellationToken>())
            .Returns(existingToken);
        _userRepo.GetByIdAsync(user.Id, Arg.Any<CancellationToken>())
            .Returns(user);
        _workspaceQueryService.ResolveWorkspaceContextAsync(user, unauthorizedProjectId, true, Arg.Any<CancellationToken>())
            .Returns<UserWorkspaceContextDto>(_ => throw new UnauthorizedException("User does not have access to the specified project."));

        // Act
        var act = () => CreateSut(executionContextProjectId: unauthorizedProjectId).ExecuteAsync(new RefreshTokenCommand(rawToken));

        // Assert
        await act.Should().ThrowAsync<UnauthorizedException>()
            .WithMessage("User does not have access to the specified project.");
        await _uow.DidNotReceive().CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenSuperAdminWithProjectHeader_AllowsAnyActiveProject()
    {
        // Arrange
        var now = TestConstants.BaselineTime;
        var rawToken = "admin-refresh-token";
        var tokenHash = RefreshTokenHelper.HashToken(rawToken);
        User user = aUser.Active().WithSystemRole(SystemRole.SuperAdmin).WithTime(now);
        var existingToken = RefreshTokenEntity.Create(user.Id, tokenHash, now.AddDays(7), now);
        var requestedProjectId = Guid.NewGuid();

        var accessibleProjects = new List<AccessibleProjectDto>
        {
            new(requestedProjectId, "Target Project", "target", "Admin")
        };

        var workspaceContext = new UserWorkspaceContextDto(requestedProjectId, "Admin", requestedProjectId, accessibleProjects);

        _tokenRepo.GetByHashAsync(tokenHash, Arg.Any<CancellationToken>())
            .Returns(existingToken);
        _userRepo.GetByIdAsync(user.Id, Arg.Any<CancellationToken>())
            .Returns(user);
        _workspaceQueryService.ResolveWorkspaceContextAsync(user, requestedProjectId, true, Arg.Any<CancellationToken>())
            .Returns(workspaceContext);
        _jwtGenerator.GenerateToken(user, requestedProjectId, Arg.Any<IEnumerable<string>>())
            .Returns("admin-jwt-token");

        // Act
        var result = await CreateSut(executionContextProjectId: requestedProjectId).ExecuteAsync(new RefreshTokenCommand(rawToken));

        // Assert
        result.Should().NotBeNull();
        result.AccessToken.Should().Be("admin-jwt-token");
        _jwtGenerator.Received(1).GenerateToken(user, requestedProjectId, Arg.Is<IEnumerable<string>>(roles => roles.Contains("Admin")));
    }

    [Fact]
    public async Task ExecuteAsync_WhenTokenNotFound_ThrowsUnauthorizedException()
    {
        // Arrange
        var rawToken = "non-existent-token";
        var tokenHash = RefreshTokenHelper.HashToken(rawToken);

        _tokenRepo.GetByHashAsync(tokenHash, Arg.Any<CancellationToken>())
            .Returns((RefreshTokenEntity?)null);

        // Act
        var act = () => CreateSut().ExecuteAsync(new RefreshTokenCommand(rawToken));

        // Assert
        await act.Should().ThrowAsync<UnauthorizedException>()
            .WithMessage("*Invalid refresh token*");
        await _uow.DidNotReceive().CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenTokenAlreadyRevoked_TriggersReuseDetectionAndRevokesAllSessions()
    {
        // Arrange
        var now = TestConstants.BaselineTime;
        var rawToken = "revoked-token";
        var tokenHash = RefreshTokenHelper.HashToken(rawToken);
        var userId = Guid.NewGuid();
        var revokedToken = RefreshTokenEntity.Create(new UserId(userId), tokenHash, now.AddDays(7), now);
        revokedToken.Revoke(now.AddMinutes(-5));

        _tokenRepo.GetByHashAsync(tokenHash, Arg.Any<CancellationToken>())
            .Returns(revokedToken);

        // Act
        var act = () => CreateSut().ExecuteAsync(new RefreshTokenCommand(rawToken));

        // Assert
        await act.Should().ThrowAsync<UnauthorizedException>()
            .WithMessage("*reuse detected*");
        await _tokenRepo.Received(1).RevokeAllByUserIdAsync(new UserId(userId), now, Arg.Any<CancellationToken>());
        await _uow.Received(1).CommitAsync(Arg.Any<CancellationToken>());
        _auditService.Received(1).LogTokenReuseDetected(userId);
    }

    [Fact]
    public async Task ExecuteAsync_WhenRevokedTokenReusedWithinLeeway_SucceedsAndIssuesNewToken()
    {
        // Arrange
        var now = TestConstants.BaselineTime;
        var rawToken = "concurrent-tab-token";
        var tokenHash = RefreshTokenHelper.HashToken(rawToken);
        User user = aUser.Active().WithTime(now);
        var revokedToken = RefreshTokenEntity.Create(user.Id, tokenHash, now.AddDays(7), now);
        // Revoked 5 seconds ago (within the 30s leeway window)
        revokedToken.Revoke(now.AddSeconds(-5));

        var projectAlphaId = Guid.NewGuid();
        var accessibleProjects = new List<AccessibleProjectDto>
        {
            new(projectAlphaId, "Project Alpha", "alpha", "Admin")
        };
        var workspaceContext = new UserWorkspaceContextDto(projectAlphaId, "Admin", projectAlphaId, accessibleProjects);

        _tokenRepo.GetByHashAsync(tokenHash, Arg.Any<CancellationToken>())
            .Returns(revokedToken);
        _userRepo.GetByIdAsync(user.Id, Arg.Any<CancellationToken>())
            .Returns(user);
        _workspaceQueryService.ResolveWorkspaceContextAsync(user, null, true, Arg.Any<CancellationToken>())
            .Returns(workspaceContext);
        _jwtGenerator.GenerateToken(user, projectAlphaId, Arg.Any<IEnumerable<string>>())
            .Returns("grace-period-jwt");

        // Act
        var result = await CreateSut().ExecuteAsync(new RefreshTokenCommand(rawToken));

        // Assert
        result.Should().NotBeNull();
        result.AccessToken.Should().Be("grace-period-jwt");
        result.RefreshToken.Should().NotBeNullOrWhiteSpace();
        result.RefreshToken.Should().NotBe(rawToken);

        // Crucial: Must NOT revoke all user sessions when within leeway
        await _tokenRepo.DidNotReceive().RevokeAllByUserIdAsync(user.Id, Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>());
        await _tokenRepo.Received(1).AddAsync(Arg.Is<RefreshTokenEntity>(t => t.UserId == user.Id), Arg.Any<CancellationToken>());
        await _uow.Received(1).CommitAsync(Arg.Any<CancellationToken>());
        _auditService.Received(1).LogTokenRotated(user.Id.Value);
        _auditService.DidNotReceive().LogTokenReuseDetected(Arg.Any<Guid>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenTokenExpired_ThrowsUnauthorizedException()
    {
        // Arrange
        var now = TestConstants.BaselineTime;
        var rawToken = "expired-token";
        var tokenHash = RefreshTokenHelper.HashToken(rawToken);
        var userId = Guid.NewGuid();
        var expiredToken = RefreshTokenEntity.Create(new UserId(userId), tokenHash, now.AddMinutes(10), now);
        _timeProvider.Advance(TimeSpan.FromMinutes(20));

        _tokenRepo.GetByHashAsync(tokenHash, Arg.Any<CancellationToken>())
            .Returns(expiredToken);

        // Act
        var act = () => CreateSut().ExecuteAsync(new RefreshTokenCommand(rawToken));

        // Assert
        await act.Should().ThrowAsync<UnauthorizedException>()
            .WithMessage("*expired*");
        await _uow.DidNotReceive().CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenUserInactive_ThrowsUnauthorizedException()
    {
        // Arrange
        var now = TestConstants.BaselineTime;
        var rawToken = "inactive-user-token";
        var tokenHash = RefreshTokenHelper.HashToken(rawToken);
        User user = aUser.Inactive().WithTime(now);
        var existingToken = RefreshTokenEntity.Create(user.Id, tokenHash, now.AddDays(7), now);

        _tokenRepo.GetByHashAsync(tokenHash, Arg.Any<CancellationToken>())
            .Returns(existingToken);
        _userRepo.GetByIdAsync(user.Id, Arg.Any<CancellationToken>())
            .Returns(user);

        // Act
        var act = () => CreateSut().ExecuteAsync(new RefreshTokenCommand(rawToken));

        // Assert
        await act.Should().ThrowAsync<UnauthorizedException>()
            .WithMessage("*inactive or not found*");
        await _uow.DidNotReceive().CommitAsync(Arg.Any<CancellationToken>());
    }
}
