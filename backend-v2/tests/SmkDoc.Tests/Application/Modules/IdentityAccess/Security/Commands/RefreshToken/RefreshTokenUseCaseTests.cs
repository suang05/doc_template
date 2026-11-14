using SmkDoc.Application.Common.Constants;
using SmkDoc.Application.Modules.IdentityAccess.Security.Commands.RefreshToken;
using SmkDoc.Application.Modules.IdentityAccess.Security.Services;
using SmkDoc.Domain.ValueObjects.Ids;

namespace SmkDoc.Tests.Application.Modules.IdentityAccess.Security.Commands.RefreshToken;

public sealed class RefreshTokenUseCaseTests
{
    private readonly ITokenSessionGenerator _tokenSessionGenerator = Substitute.For<ITokenSessionGenerator>();
    private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();
    private readonly ISecurityAuditService _auditService = Substitute.For<ISecurityAuditService>();

    public RefreshTokenUseCaseTests()
    {
        _uow.CommitAsync(Arg.Any<CancellationToken>()).Returns(1);
    }

    private RefreshTokenUseCase CreateSut() =>
        new(_tokenSessionGenerator, _uow, _auditService);

    [Fact]
    public async Task ExecuteAsync_WhenValidToken_RotatesTokenCommitsAndReturnsResult()
    {
        // Arrange
        var userId = new UserId(Guid.NewGuid());
        var rawToken = "my-secret-refresh-token";
        var rotated = new RotatedTokensResult(userId, "new_access_token", "new_refresh_token");

        _tokenSessionGenerator.RotateTokensAsync(rawToken, ct: Arg.Any<CancellationToken>())
            .Returns(rotated);

        // Act
        var sut = CreateSut();
        var result = await sut.ExecuteAsync(new RefreshTokenCommand(rawToken), CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.AccessToken.Should().Be("new_access_token");
        result.RefreshToken.Should().Be("new_refresh_token");
        result.ExpiresIn.Should().Be(AuthConstants.AccessTokenLifetimeSeconds);
        result.TokenType.Should().Be(AuthConstants.BearerTokenType);

        await _uow.Received(1).CommitAsync(Arg.Any<CancellationToken>());
        _auditService.Received(1).LogTokenRotated(userId.Value);
    }

    [Fact]
    public async Task ExecuteAsync_WhenTokenCompromised_RevokesAllSessionsLogsAuditAndThrowsUnauthorized()
    {
        // Arrange
        var userId = new UserId(Guid.NewGuid());
        var rawToken = "reused-stolen-token";

        _tokenSessionGenerator.RotateTokensAsync(rawToken, ct: Arg.Any<CancellationToken>())
            .ThrowsAsync(new TokenReuseCompromiseException(userId));

        // Act
        var sut = CreateSut();
        var act = () => sut.ExecuteAsync(new RefreshTokenCommand(rawToken), CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<UnauthorizedException>()
            .WithMessage("Invalid refresh token (reuse detected).");

        _auditService.Received(1).LogTokenReuseDetected(userId.Value);
        await _uow.Received(1).CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenTokenInvalidOrExpired_ThrowsUnauthorizedWithoutCommit()
    {
        // Arrange
        var rawToken = "invalid-token";

        _tokenSessionGenerator.RotateTokensAsync(rawToken, ct: Arg.Any<CancellationToken>())
            .ThrowsAsync(new UnauthorizedException("Invalid refresh token."));

        // Act
        var sut = CreateSut();
        var act = () => sut.ExecuteAsync(new RefreshTokenCommand(rawToken), CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<UnauthorizedException>()
            .WithMessage("Invalid refresh token.");

        await _uow.DidNotReceive().CommitAsync(Arg.Any<CancellationToken>());
        _auditService.DidNotReceive().LogTokenRotated(Arg.Any<Guid>());
    }
}
