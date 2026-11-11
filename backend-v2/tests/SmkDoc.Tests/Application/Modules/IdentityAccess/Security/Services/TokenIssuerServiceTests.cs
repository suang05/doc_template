using FluentAssertions;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using SmkDoc.Application.Common.Constants;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.IdentityAccess.Security.DTOs;
using SmkDoc.Application.Modules.IdentityAccess.Security.Helpers;
using SmkDoc.Application.Modules.IdentityAccess.Security.Services;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Enums;
using SmkDoc.Domain.Exceptions;
using SmkDoc.Domain.Interfaces;
using SmkDoc.Domain.ValueObjects;
using SmkDoc.Domain.ValueObjects.Ids;
using SmkDoc.Tests.Common.Builders;
using Xunit;
using RefreshTokenEntity = SmkDoc.Domain.Entities.RefreshToken;

namespace SmkDoc.Tests.Application.Modules.IdentityAccess.Security.Services;

public sealed class TokenIssuerServiceTests
{
    private readonly IRefreshTokenRepository _tokenRepo = Substitute.For<IRefreshTokenRepository>();
    private readonly IUserRepository _userRepo = Substitute.For<IUserRepository>();
    private readonly IJwtTokenGenerator _jwtGenerator = Substitute.For<IJwtTokenGenerator>();
    private readonly IUserWorkspaceQueryService _workspaceQueryService = Substitute.For<IUserWorkspaceQueryService>();
    private readonly FakeTimeProvider _timeProvider = TestConstants.CreateFakeClock();

    private TokenIssuerService CreateSut() =>
        new(_tokenRepo, _userRepo, _jwtGenerator, _workspaceQueryService, _timeProvider);

    [Fact]
    public async Task IssueTokensAsync_WhenValidUser_GeneratesTokensAndAddsRefreshToken()
    {
        // Arrange
        var now = TestConstants.BaselineTime;
        var projectId = Guid.NewGuid();
        User user = aUser.Active().WithTime(now);

        var accessibleProjects = new List<AccessibleProjectResultDto>
        {
            new(projectId, "Project Alpha", "project-alpha", "Admin")
        };
        var workspaceContext = new UserWorkspaceContextResultDto(projectId, "Admin", projectId, accessibleProjects);

        _workspaceQueryService.ResolveWorkspaceContextAsync(user, projectId, true, Arg.Any<CancellationToken>())
            .Returns(workspaceContext);

        _jwtGenerator.GenerateToken(user, projectId, Arg.Any<IEnumerable<string>>())
            .Returns("access_token_123");

        var activeKeys = new List<ApiKeyResultDto>
        {
            new(Guid.NewGuid(), "Master Key", "ERP", true, null, now, "ReadWrite", "smk_erp_****")
        };
        _workspaceQueryService.GetActiveApiKeysAsync(projectId, Arg.Any<CancellationToken>())
            .Returns(activeKeys);

        // Act
        var sut = CreateSut();
        var result = await sut.IssueTokensAsync(user, projectId);

        // Assert
        result.Should().NotBeNull();
        result.AccessToken.Should().Be("access_token_123");
        result.RefreshToken.Should().NotBeNullOrWhiteSpace();
        result.Workspace.ActiveProjectId.Should().Be(projectId);
        result.ActiveApiKeys.Should().HaveCount(1);
        await _tokenRepo.Received(1).AddAsync(Arg.Is<RefreshTokenEntity>(t => t.UserId == user.Id), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RotateTokensAsync_WhenValidToken_RevokesOldAndIssuesNewTokens()
    {
        // Arrange
        var now = TestConstants.BaselineTime;
        var rawToken = "valid-refresh-token";
        var tokenHash = RefreshTokenGenerator.HashToken(rawToken);
        User user = aUser.Active().WithTime(now);
        var existingToken = RefreshTokenEntity.Create(user.Id, tokenHash, now.AddDays(7), now);

        _tokenRepo.GetByHashAsync(tokenHash, Arg.Any<CancellationToken>())
            .Returns(existingToken);
        _userRepo.GetByIdAsync(user.Id, Arg.Any<CancellationToken>())
            .Returns(user);

        var workspaceContext = new UserWorkspaceContextResultDto(Guid.NewGuid(), "Admin", Guid.NewGuid(), []);
        _workspaceQueryService.ResolveWorkspaceContextAsync(user, Arg.Any<Guid?>(), true, Arg.Any<CancellationToken>())
            .Returns(workspaceContext);

        _jwtGenerator.GenerateToken(user, Arg.Any<Guid?>(), Arg.Any<IEnumerable<string>>())
            .Returns("new_access_token");

        // Act
        var sut = CreateSut();
        var result = await sut.RotateTokensAsync(rawToken);

        // Assert
        result.Should().NotBeNull();
        result.AccessToken.Should().Be("new_access_token");
        result.NewRefreshToken.Should().NotBeNullOrWhiteSpace();
        result.NewRefreshToken.Should().NotBe(rawToken);
        existingToken.IsRevoked.Should().BeTrue();
        await _tokenRepo.Received(1).AddAsync(Arg.Is<RefreshTokenEntity>(t => t.UserId == user.Id), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RotateTokensAsync_WhenTokenNotFound_ThrowsUnauthorizedException()
    {
        // Arrange
        var rawToken = "unknown-token";
        _tokenRepo.GetByHashAsync(Arg.Any<Sha256Hash>(), Arg.Any<CancellationToken>())
            .Returns((RefreshTokenEntity?)null);

        // Act
        var sut = CreateSut();
        var act = () => sut.RotateTokensAsync(rawToken);

        // Assert
        await act.Should().ThrowAsync<UnauthorizedException>()
            .WithMessage("Invalid refresh token.");
    }

    [Fact]
    public async Task RotateTokensAsync_WhenRevokedBeyondLeeway_RevokesAllSessionsAndThrowsCompromise()
    {
        // Arrange
        var now = TestConstants.BaselineTime;
        var rawToken = "stolen-refresh-token";
        var tokenHash = RefreshTokenGenerator.HashToken(rawToken);
        var userId = new UserId(Guid.NewGuid());

        var token = new RefreshTokenEntity(
            Guid.NewGuid(),
            userId,
            tokenHash,
            now.AddDays(7),
            now.AddDays(-2),
            revokedAt: now.AddSeconds(-300),
            replacedByTokenHash: RefreshTokenGenerator.HashToken("replacement_token"));

        _tokenRepo.GetByHashAsync(tokenHash, Arg.Any<CancellationToken>())
            .Returns(token);

        // Act
        var sut = CreateSut();
        var act = () => sut.RotateTokensAsync(rawToken);

        // Assert
        var ex = await act.Should().ThrowAsync<TokenReuseCompromiseException>();
        ex.Which.UserId.Should().Be(userId);
        await _tokenRepo.Received(1).RevokeAllByUserIdAsync(userId, now, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RotateTokensAsync_WhenExpired_ThrowsUnauthorizedException()
    {
        // Arrange
        var now = TestConstants.BaselineTime;
        var rawToken = "expired-token";
        var tokenHash = RefreshTokenGenerator.HashToken(rawToken);
        var userId = new UserId(Guid.NewGuid());

        var token = new RefreshTokenEntity(
            Guid.NewGuid(),
            userId,
            tokenHash,
            expiresAt: now.AddSeconds(-1),
            now: now.AddDays(-10));

        _tokenRepo.GetByHashAsync(tokenHash, Arg.Any<CancellationToken>())
            .Returns(token);

        // Act
        var sut = CreateSut();
        var act = () => sut.RotateTokensAsync(rawToken);

        // Assert
        await act.Should().ThrowAsync<UnauthorizedException>()
            .WithMessage("Refresh token has expired.");
    }

    [Fact]
    public async Task RotateTokensAsync_WhenUserInactive_ThrowsUnauthorizedException()
    {
        // Arrange
        var now = TestConstants.BaselineTime;
        var rawToken = "valid-token-inactive-user";
        var tokenHash = RefreshTokenGenerator.HashToken(rawToken);
        User user = aUser.Inactive().WithTime(now);
        var token = RefreshTokenEntity.Create(user.Id, tokenHash, now.AddDays(7), now);

        _tokenRepo.GetByHashAsync(tokenHash, Arg.Any<CancellationToken>())
            .Returns(token);
        _userRepo.GetByIdAsync(user.Id, Arg.Any<CancellationToken>())
            .Returns(user);

        // Act
        var sut = CreateSut();
        var act = () => sut.RotateTokensAsync(rawToken);

        // Assert
        await act.Should().ThrowAsync<UnauthorizedException>()
            .WithMessage("User account is inactive or not found.");
    }
}
