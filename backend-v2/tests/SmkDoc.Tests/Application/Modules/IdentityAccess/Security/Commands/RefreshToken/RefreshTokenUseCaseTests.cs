using FluentAssertions;
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
using Xunit;
using RefreshTokenEntity = SmkDoc.Domain.Entities.RefreshToken;

namespace SmkDoc.Tests.Application.Modules.IdentityAccess.Security.Commands.RefreshToken;

public class RefreshTokenUseCaseTests
{
    private readonly Mock<IRefreshTokenRepository> _tokenRepoMock = new();
    private readonly Mock<IUserRepository> _userRepoMock = new();
    private readonly Mock<IJwtTokenGenerator> _jwtGeneratorMock = new();
    private readonly Mock<IUserWorkspaceQueryService> _workspaceQueryServiceMock = new();
    private readonly Mock<IUnitOfWork> _uowMock = new();
    private readonly Mock<IExecutionContext> _executionContextMock = new();
    private readonly FakeTimeProvider _timeProvider = new(TestConstants.BaselineTime);

    private RefreshTokenUseCase CreateSut(Guid? executionContextProjectId = null)
    {
        _executionContextMock.Setup(x => x.ProjectId).Returns(executionContextProjectId);

        return new(
            _tokenRepoMock.Object,
            _userRepoMock.Object,
            _jwtGeneratorMock.Object,
            _workspaceQueryServiceMock.Object,
            _uowMock.Object,
            validator: null,
            timeProvider: _timeProvider,
            executionContext: _executionContextMock.Object);
    }

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

        _tokenRepoMock.Setup(r => r.GetByHashAsync(tokenHash, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingToken);
        _userRepoMock.Setup(r => r.GetByIdAsync(user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        _workspaceQueryServiceMock.Setup(q => q.GetAccessibleProjectsAsync(user.Id, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(accessibleProjects);
        _jwtGeneratorMock.Setup(j => j.GenerateToken(user, projectAlphaId, It.IsAny<IEnumerable<string>>()))
            .Returns("new-jwt-token");

        // Act
        var result = await CreateSut().ExecuteAsync(new RefreshTokenCommand(rawToken));

        // Assert
        result.Should().NotBeNull();
        result.AccessToken.Should().Be("new-jwt-token");
        result.RefreshToken.Should().NotBeNullOrWhiteSpace();
        result.RefreshToken.Should().NotBe(rawToken);

        existingToken.IsRevoked.Should().BeTrue();
        _tokenRepoMock.Verify(r => r.AddAsync(It.Is<RefreshTokenEntity>(t => t.UserId == user.Id), It.IsAny<CancellationToken>()), Times.Once);
        _uowMock.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
        _jwtGeneratorMock.Verify(j => j.GenerateToken(user, projectAlphaId, It.Is<IEnumerable<string>>(roles => roles.Contains("Admin"))), Times.Once);
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

        _tokenRepoMock.Setup(r => r.GetByHashAsync(tokenHash, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingToken);
        _userRepoMock.Setup(r => r.GetByIdAsync(user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        _workspaceQueryServiceMock.Setup(q => q.GetAccessibleProjectsAsync(user.Id, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(accessibleProjects);
        _jwtGeneratorMock.Setup(j => j.GenerateToken(user, projectBetaId, It.IsAny<IEnumerable<string>>()))
            .Returns("refreshed-beta-jwt");

        // Act
        var result = await CreateSut(executionContextProjectId: projectBetaId).ExecuteAsync(new RefreshTokenCommand(rawToken));

        // Assert
        result.Should().NotBeNull();
        result.AccessToken.Should().Be("refreshed-beta-jwt");
        _jwtGeneratorMock.Verify(j => j.GenerateToken(user, projectBetaId, It.Is<IEnumerable<string>>(roles => roles.Contains("Viewer"))), Times.Once);
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
        var accessibleProjects = new List<AccessibleProjectDto>
        {
            new(Guid.NewGuid(), "Authorized Project", "auth", "Viewer")
        };

        _tokenRepoMock.Setup(r => r.GetByHashAsync(tokenHash, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingToken);
        _userRepoMock.Setup(r => r.GetByIdAsync(user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        _workspaceQueryServiceMock.Setup(q => q.GetAccessibleProjectsAsync(user.Id, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(accessibleProjects);

        // Act
        var act = () => CreateSut(executionContextProjectId: unauthorizedProjectId).ExecuteAsync(new RefreshTokenCommand(rawToken));

        // Assert
        await act.Should().ThrowAsync<UnauthorizedException>()
            .WithMessage("User does not have access to the specified project.");
        _uowMock.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
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

        _tokenRepoMock.Setup(r => r.GetByHashAsync(tokenHash, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingToken);
        _userRepoMock.Setup(r => r.GetByIdAsync(user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        _workspaceQueryServiceMock.Setup(q => q.GetAccessibleProjectsAsync(user.Id, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(accessibleProjects);
        _jwtGeneratorMock.Setup(j => j.GenerateToken(user, requestedProjectId, It.IsAny<IEnumerable<string>>()))
            .Returns("admin-jwt-token");

        // Act
        var result = await CreateSut(executionContextProjectId: requestedProjectId).ExecuteAsync(new RefreshTokenCommand(rawToken));

        // Assert
        result.Should().NotBeNull();
        result.AccessToken.Should().Be("admin-jwt-token");
        _jwtGeneratorMock.Verify(j => j.GenerateToken(user, requestedProjectId, It.Is<IEnumerable<string>>(roles => roles.Contains("Admin"))), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_WhenTokenNotFound_ThrowsUnauthorizedException()
    {
        // Arrange
        var rawToken = "non-existent-token";
        var tokenHash = RefreshTokenHelper.HashToken(rawToken);

        _tokenRepoMock.Setup(r => r.GetByHashAsync(tokenHash, It.IsAny<CancellationToken>()))
            .ReturnsAsync((RefreshTokenEntity?)null);

        // Act
        var act = () => CreateSut().ExecuteAsync(new RefreshTokenCommand(rawToken));

        // Assert
        await act.Should().ThrowAsync<UnauthorizedException>()
            .WithMessage("*Invalid refresh token*");
        _uowMock.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
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
        revokedToken.Revoke(now.AddMinutes(5));

        _tokenRepoMock.Setup(r => r.GetByHashAsync(tokenHash, It.IsAny<CancellationToken>()))
            .ReturnsAsync(revokedToken);

        // Act
        var act = () => CreateSut().ExecuteAsync(new RefreshTokenCommand(rawToken));

        // Assert
        await act.Should().ThrowAsync<UnauthorizedException>()
            .WithMessage("*reuse detected*");
        _tokenRepoMock.Verify(r => r.RevokeAllByUserIdAsync(userId, now, It.IsAny<CancellationToken>()), Times.Once);
        _uowMock.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
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
        _timeProvider.Advance(TimeSpan.FromMinutes(20));

        _tokenRepoMock.Setup(r => r.GetByHashAsync(tokenHash, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expiredToken);

        // Act
        var act = () => CreateSut().ExecuteAsync(new RefreshTokenCommand(rawToken));

        // Assert
        await act.Should().ThrowAsync<UnauthorizedException>()
            .WithMessage("*expired*");
        _uowMock.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
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

        _tokenRepoMock.Setup(r => r.GetByHashAsync(tokenHash, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingToken);
        _userRepoMock.Setup(r => r.GetByIdAsync(user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        // Act
        var act = () => CreateSut().ExecuteAsync(new RefreshTokenCommand(rawToken));

        // Assert
        await act.Should().ThrowAsync<UnauthorizedException>()
            .WithMessage("*inactive or not found*");
        _uowMock.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
