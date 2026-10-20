using FluentAssertions;
using FluentValidation;
using Microsoft.Extensions.Time.Testing;
using Moq;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.IdentityAccess.Security.Commands.RefreshToken;
using SmkDoc.Application.Modules.IdentityAccess.Security.DTOs;
using SmkDoc.Application.Modules.IdentityAccess.Security.Helpers;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Enums;
using SmkDoc.Domain.Exceptions;
using SmkDoc.Domain.Interfaces;
using SmkDoc.Tests.Common.Builders;
using SmkDoc.Tests.Common.Fixtures;
using Xunit;
using RefreshTokenEntity = SmkDoc.Domain.Entities.RefreshToken;

namespace SmkDoc.Tests.Application.Modules.IdentityAccess.Security.Commands.RefreshToken;

public class RefreshTokenUseCaseTests
{
    private readonly SecurityModuleTestFixture _fixture = new();

    private RefreshTokenUseCase CreateSut(Guid? executionContextProjectId = null) =>
        _fixture.BuildRefreshTokenUseCase(executionContextProjectId);

    [Fact]
    public async Task ExecuteAsync_WhenValidTokenAndNoProjectHeader_RotatesTokenAndDefaultsToFirstAccessibleProject()
    {
        // Arrange
        var now = TestConstants.BaselineTime;
        var rawToken = "my-secret-refresh-token";
        var tokenHash = RefreshTokenHelper.HashToken(rawToken);
        var user = UserBuilder.AUser().WithTime(now).Build();
        var existingToken = RefreshTokenEntity.Create(user.Id, tokenHash, now.AddDays(7), now);
        var projectAlphaId = Guid.NewGuid();

        var accessibleProjects = new List<AccessibleProjectDto>
        {
            new(projectAlphaId, "Project Alpha", "alpha", "Admin")
        };

        var workspaceContext = new UserWorkspaceContextDto(projectAlphaId, "Admin", projectAlphaId, accessibleProjects);

        _fixture.TokenRepo.Setup(r => r.GetByHashAsync(tokenHash, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingToken);
        _fixture.UserRepo.Setup(r => r.GetByIdAsync(user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        _fixture.WorkspaceQueryService.Setup(q => q.ResolveWorkspaceContextAsync(user, null, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(workspaceContext);
        _fixture.JwtGenerator.Setup(j => j.GenerateToken(user, projectAlphaId, It.IsAny<IEnumerable<string>>()))
            .Returns("new-jwt-token");

        // Act
        var result = await CreateSut().ExecuteAsync(new RefreshTokenCommand(rawToken));

        // Assert
        result.Should().NotBeNull();
        result.AccessToken.Should().Be("new-jwt-token");
        result.RefreshToken.Should().NotBeNullOrWhiteSpace();
        result.RefreshToken.Should().NotBe(rawToken);

        existingToken.IsRevoked.Should().BeTrue();
        _fixture.TokenRepo.Verify(r => r.AddAsync(It.Is<RefreshTokenEntity>(t => t.UserId == user.Id), It.IsAny<CancellationToken>()), Times.Once);
        _fixture.Uow.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
        _fixture.JwtGenerator.Verify(j => j.GenerateToken(user, projectAlphaId, It.Is<IEnumerable<string>>(roles => roles.Contains("Admin"))), Times.Once);
        _fixture.AuditService.Verify(a => a.LogTokenRotated(user.Id), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_WhenValidTokenAndProjectHeaderSpecified_RotatesTokenAndPreservesSelectedProjectAndRole()
    {
        // Arrange
        var now = TestConstants.BaselineTime;
        var rawToken = "my-secret-refresh-token";
        var tokenHash = RefreshTokenHelper.HashToken(rawToken);
        var user = UserBuilder.AUser().WithTime(now).Build();
        var existingToken = RefreshTokenEntity.Create(user.Id, tokenHash, now.AddDays(7), now);

        var projectAlphaId = Guid.NewGuid();
        var projectBetaId = Guid.NewGuid();

        var accessibleProjects = new List<AccessibleProjectDto>
        {
            new(projectAlphaId, "Project Alpha", "alpha", "Admin"),
            new(projectBetaId, "Project Beta", "beta", "Viewer")
        };

        var workspaceContext = new UserWorkspaceContextDto(projectBetaId, "Viewer", projectAlphaId, accessibleProjects);

        _fixture.TokenRepo.Setup(r => r.GetByHashAsync(tokenHash, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingToken);
        _fixture.UserRepo.Setup(r => r.GetByIdAsync(user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        _fixture.WorkspaceQueryService.Setup(q => q.ResolveWorkspaceContextAsync(user, projectBetaId, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(workspaceContext);
        _fixture.JwtGenerator.Setup(j => j.GenerateToken(user, projectBetaId, It.IsAny<IEnumerable<string>>()))
            .Returns("refreshed-beta-jwt");

        // Act
        var result = await CreateSut(executionContextProjectId: projectBetaId).ExecuteAsync(new RefreshTokenCommand(rawToken));

        // Assert
        result.Should().NotBeNull();
        result.AccessToken.Should().Be("refreshed-beta-jwt");
        _fixture.JwtGenerator.Verify(j => j.GenerateToken(user, projectBetaId, It.Is<IEnumerable<string>>(roles => roles.Contains("Viewer"))), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_WhenValidTokenAndUnauthorizedProjectHeaderSpecified_ThrowsUnauthorizedException()
    {
        // Arrange
        var now = TestConstants.BaselineTime;
        var rawToken = "my-secret-refresh-token";
        var tokenHash = RefreshTokenHelper.HashToken(rawToken);
        var user = UserBuilder.AUser().WithSystemRole(SystemRole.Member).WithTime(now).Build();
        var existingToken = RefreshTokenEntity.Create(user.Id, tokenHash, now.AddDays(7), now);

        var unauthorizedProjectId = Guid.NewGuid();

        _fixture.TokenRepo.Setup(r => r.GetByHashAsync(tokenHash, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingToken);
        _fixture.UserRepo.Setup(r => r.GetByIdAsync(user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        _fixture.WorkspaceQueryService.Setup(q => q.ResolveWorkspaceContextAsync(user, unauthorizedProjectId, true, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new UnauthorizedException("User does not have access to the specified project."));

        // Act
        var act = () => CreateSut(executionContextProjectId: unauthorizedProjectId).ExecuteAsync(new RefreshTokenCommand(rawToken));

        // Assert
        await act.Should().ThrowAsync<UnauthorizedException>()
            .WithMessage("User does not have access to the specified project.");
        _fixture.Uow.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_WhenSuperAdminWithProjectHeader_AllowsAnyActiveProject()
    {
        // Arrange
        var now = TestConstants.BaselineTime;
        var rawToken = "admin-refresh-token";
        var tokenHash = RefreshTokenHelper.HashToken(rawToken);
        var user = UserBuilder.AUser().WithSystemRole(SystemRole.SuperAdmin).WithTime(now).Build();
        var existingToken = RefreshTokenEntity.Create(user.Id, tokenHash, now.AddDays(7), now);
        var requestedProjectId = Guid.NewGuid();

        var accessibleProjects = new List<AccessibleProjectDto>
        {
            new(requestedProjectId, "Target Project", "target", "Admin")
        };

        var workspaceContext = new UserWorkspaceContextDto(requestedProjectId, "Admin", requestedProjectId, accessibleProjects);

        _fixture.TokenRepo.Setup(r => r.GetByHashAsync(tokenHash, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingToken);
        _fixture.UserRepo.Setup(r => r.GetByIdAsync(user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        _fixture.WorkspaceQueryService.Setup(q => q.ResolveWorkspaceContextAsync(user, requestedProjectId, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(workspaceContext);
        _fixture.JwtGenerator.Setup(j => j.GenerateToken(user, requestedProjectId, It.IsAny<IEnumerable<string>>()))
            .Returns("admin-jwt-token");

        // Act
        var result = await CreateSut(executionContextProjectId: requestedProjectId).ExecuteAsync(new RefreshTokenCommand(rawToken));

        // Assert
        result.Should().NotBeNull();
        result.AccessToken.Should().Be("admin-jwt-token");
        _fixture.JwtGenerator.Verify(j => j.GenerateToken(user, requestedProjectId, It.Is<IEnumerable<string>>(roles => roles.Contains("Admin"))), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_WhenTokenNotFound_ThrowsUnauthorizedException()
    {
        // Arrange
        var rawToken = "non-existent-token";
        var tokenHash = RefreshTokenHelper.HashToken(rawToken);

        _fixture.TokenRepo.Setup(r => r.GetByHashAsync(tokenHash, It.IsAny<CancellationToken>()))
            .ReturnsAsync((RefreshTokenEntity?)null);

        // Act
        var act = () => CreateSut().ExecuteAsync(new RefreshTokenCommand(rawToken));

        // Assert
        await act.Should().ThrowAsync<UnauthorizedException>()
            .WithMessage("*Invalid refresh token*");
        _fixture.Uow.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_WhenTokenAlreadyRevoked_TriggersReuseDetectionAndRevokesAllSessions()
    {
        // Arrange
        var now = TestConstants.BaselineTime;
        var rawToken = "revoked-token";
        var tokenHash = RefreshTokenHelper.HashToken(rawToken);
        var userId = Guid.NewGuid();
        var revokedToken = RefreshTokenEntity.Create(userId, tokenHash, now.AddDays(7), now);
        revokedToken.Revoke(now.AddMinutes(-5));

        _fixture.TokenRepo.Setup(r => r.GetByHashAsync(tokenHash, It.IsAny<CancellationToken>()))
            .ReturnsAsync(revokedToken);

        // Act
        var act = () => CreateSut().ExecuteAsync(new RefreshTokenCommand(rawToken));

        // Assert
        await act.Should().ThrowAsync<UnauthorizedException>()
            .WithMessage("*reuse detected*");
        _fixture.TokenRepo.Verify(r => r.RevokeAllByUserIdAsync(userId, now, It.IsAny<CancellationToken>()), Times.Once);
        _fixture.Uow.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
        _fixture.AuditService.Verify(a => a.LogTokenReuseDetected(userId), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_WhenRevokedTokenReusedWithinLeeway_SucceedsAndIssuesNewToken()
    {
        // Arrange
        var now = TestConstants.BaselineTime;
        var rawToken = "concurrent-tab-token";
        var tokenHash = RefreshTokenHelper.HashToken(rawToken);
        var user = UserBuilder.AUser().WithTime(now).Build();
        var revokedToken = RefreshTokenEntity.Create(user.Id, tokenHash, now.AddDays(7), now);
        // Revoked 5 seconds ago (within the 30s leeway window)
        revokedToken.Revoke(now.AddSeconds(-5));

        var projectAlphaId = Guid.NewGuid();
        var accessibleProjects = new List<AccessibleProjectDto>
        {
            new(projectAlphaId, "Project Alpha", "alpha", "Admin")
        };
        var workspaceContext = new UserWorkspaceContextDto(projectAlphaId, "Admin", projectAlphaId, accessibleProjects);

        _fixture.TokenRepo.Setup(r => r.GetByHashAsync(tokenHash, It.IsAny<CancellationToken>()))
            .ReturnsAsync(revokedToken);
        _fixture.UserRepo.Setup(r => r.GetByIdAsync(user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        _fixture.WorkspaceQueryService.Setup(q => q.ResolveWorkspaceContextAsync(user, null, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(workspaceContext);
        _fixture.JwtGenerator.Setup(j => j.GenerateToken(user, projectAlphaId, It.IsAny<IEnumerable<string>>()))
            .Returns("grace-period-jwt");

        // Act
        var result = await CreateSut().ExecuteAsync(new RefreshTokenCommand(rawToken));

        // Assert
        result.Should().NotBeNull();
        result.AccessToken.Should().Be("grace-period-jwt");
        result.RefreshToken.Should().NotBeNullOrWhiteSpace();
        result.RefreshToken.Should().NotBe(rawToken);

        // Crucial: Must NOT revoke all user sessions when within leeway
        _fixture.TokenRepo.Verify(r => r.RevokeAllByUserIdAsync(user.Id, It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()), Times.Never);
        _fixture.TokenRepo.Verify(r => r.AddAsync(It.Is<RefreshTokenEntity>(t => t.UserId == user.Id), It.IsAny<CancellationToken>()), Times.Once);
        _fixture.Uow.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
        _fixture.AuditService.Verify(a => a.LogTokenRotated(user.Id), Times.Once);
        _fixture.AuditService.Verify(a => a.LogTokenReuseDetected(It.IsAny<Guid>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_WhenTokenExpired_ThrowsUnauthorizedException()
    {
        // Arrange
        var now = TestConstants.BaselineTime;
        var rawToken = "expired-token";
        var tokenHash = RefreshTokenHelper.HashToken(rawToken);
        var userId = Guid.NewGuid();
        var expiredToken = RefreshTokenEntity.Create(userId, tokenHash, now.AddMinutes(10), now);
        _fixture.TimeProvider.Advance(TimeSpan.FromMinutes(20));

        _fixture.TokenRepo.Setup(r => r.GetByHashAsync(tokenHash, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expiredToken);

        // Act
        var act = () => CreateSut().ExecuteAsync(new RefreshTokenCommand(rawToken));

        // Assert
        await act.Should().ThrowAsync<UnauthorizedException>()
            .WithMessage("*expired*");
        _fixture.Uow.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_WhenUserInactive_ThrowsUnauthorizedException()
    {
        // Arrange
        var now = TestConstants.BaselineTime;
        var rawToken = "inactive-user-token";
        var tokenHash = RefreshTokenHelper.HashToken(rawToken);
        var user = UserBuilder.AUser().WithTime(now).AsInactive().Build();
        var existingToken = RefreshTokenEntity.Create(user.Id, tokenHash, now.AddDays(7), now);

        _fixture.TokenRepo.Setup(r => r.GetByHashAsync(tokenHash, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingToken);
        _fixture.UserRepo.Setup(r => r.GetByIdAsync(user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        // Act
        var act = () => CreateSut().ExecuteAsync(new RefreshTokenCommand(rawToken));

        // Assert
        await act.Should().ThrowAsync<UnauthorizedException>()
            .WithMessage("*inactive or not found*");
        _fixture.Uow.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
