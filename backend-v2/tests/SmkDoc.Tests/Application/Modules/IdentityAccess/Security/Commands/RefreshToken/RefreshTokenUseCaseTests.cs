using FluentAssertions;
using Microsoft.Extensions.Time.Testing;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.IdentityAccess.Security.Commands.RefreshToken;
using SmkDoc.Application.Modules.IdentityAccess.Security.Helpers;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Enums;
using SmkDoc.Domain.Exceptions;
using SmkDoc.Domain.Interfaces;
using SmkDoc.Domain.ValueObjects;
using SmkDoc.Tests.Common.Builders;
using Xunit;
using RefreshTokenEntity = SmkDoc.Domain.Entities.RefreshToken;

namespace SmkDoc.Tests.Application.Modules.IdentityAccess.Security.Commands.RefreshToken;

public class RefreshTokenUseCaseTests
{
    private readonly Mock<IRefreshTokenRepository> _tokenRepoMock = new();
    private readonly Mock<IUserRepository> _userRepoMock = new();
    private readonly Mock<IUserProjectRoleRepository> _roleRepoMock = new();
    private readonly Mock<IJwtTokenGenerator> _jwtGeneratorMock = new();
    private readonly Mock<IUnitOfWork> _uowMock = new();
    private readonly FakeTimeProvider _timeProvider = new(TestConstants.BaselineTime);

    private RefreshTokenUseCase CreateSut() => new(
        _tokenRepoMock.Object,
        _userRepoMock.Object,
        _roleRepoMock.Object,
        _jwtGeneratorMock.Object,
        _uowMock.Object,
        validator: null,
        timeProvider: _timeProvider);

    [Fact]
    public async Task ExecuteAsync_WhenValidToken_RotatesTokenAndReturnsNewPair()
    {
        // Arrange
        var now = TestConstants.BaselineTime;
        var rawToken = "my-secret-refresh-token";
        var tokenHash = RefreshTokenHelper.HashToken(rawToken);
        var user = UserBuilder.AUser().WithTime(now).Build();
        var existingToken = RefreshTokenEntity.Create(user.Id, tokenHash, now.AddDays(7), now);

        _tokenRepoMock.Setup(r => r.GetByHashAsync(tokenHash, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingToken);
        _userRepoMock.Setup(r => r.GetByIdAsync(user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        _roleRepoMock.Setup(r => r.ListByUserAsync(user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<UserProjectRole>());
        _jwtGeneratorMock.Setup(j => j.GenerateToken(user, It.IsAny<Guid?>(), It.IsAny<IEnumerable<string>>()))
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
}
