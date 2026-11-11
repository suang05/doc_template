using SmkDoc.Application.Common.Constants;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.IdentityAccess.Security.DTOs;
using SmkDoc.Application.Modules.IdentityAccess.Security.Helpers;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Exceptions;
using SmkDoc.Domain.Interfaces;
using SmkDoc.Domain.ValueObjects.Ids;

namespace SmkDoc.Application.Modules.IdentityAccess.Security.Services;

/// <summary>
/// Domain application service implementing deterministic token generation, hash verification,
/// session rotation, and automated compromised session revocation.
/// </summary>
public sealed class TokenIssuerService(
    IRefreshTokenRepository tokenRepo,
    IUserRepository userRepo,
    IJwtTokenGenerator jwtTokenGenerator,
    IUserWorkspaceQueryService workspaceQueryService,
    TimeProvider timeProvider) : ITokenIssuerService
{
    public async Task<IssuedTokensResult> IssueTokensAsync(
        User user,
        Guid? preferredProjectId = null,
        CancellationToken ct = default)
    {
        var workspace = await workspaceQueryService.ResolveWorkspaceContextAsync(
            user,
            preferredProjectId,
            throwOnUnauthorized: true,
            ct);

        var roles = new List<string> { workspace.ActiveRole };
        var accessToken = jwtTokenGenerator.GenerateToken(user, workspace.ActiveProjectId, roles);

        var now = timeProvider.GetUtcNow();
        var rawRefreshToken = await CreateRefreshTokenAsync(user.Id, now, ct);

        var activeApiKeys = workspace.ActiveProjectId.HasValue
            ? await workspaceQueryService.GetActiveApiKeysAsync(workspace.ActiveProjectId.Value, ct)
            : [];

        return new IssuedTokensResult(
            accessToken,
            rawRefreshToken,
            workspace,
            activeApiKeys);
    }

    public async Task<RotatedTokensResult> RotateTokensAsync(
        string rawRefreshToken,
        Guid? preferredProjectId = null,
        CancellationToken ct = default)
    {
        var now = timeProvider.GetUtcNow();
        var token = await ValidateTokenAsync(rawRefreshToken, now, ct);
        var user = await ValidateUserAsync(token.UserId, ct);

        var workspace = await workspaceQueryService.ResolveWorkspaceContextAsync(
            user,
            preferredProjectId,
            throwOnUnauthorized: true,
            ct);

        var rawNewToken = await CreateReplacementTokenAsync(user.Id, token, now, ct);
        var roles = new List<string> { workspace.ActiveRole };
        var accessToken = jwtTokenGenerator.GenerateToken(user, workspace.ActiveProjectId, roles);

        return new RotatedTokensResult(user.Id, accessToken, rawNewToken);
    }

    private async Task<string> CreateRefreshTokenAsync(UserId userId, DateTimeOffset now, CancellationToken ct)
    {
        var raw = RefreshTokenGenerator.GenerateTokenString();
        var hash = RefreshTokenGenerator.HashToken(raw);
        var token = RefreshToken.Create(userId, hash, now.AddDays(AuthConstants.RefreshTokenLifetimeDays), now);
        await tokenRepo.AddAsync(token, ct);
        return raw;
    }

    private async Task<RefreshToken> ValidateTokenAsync(string rawRefreshToken, DateTimeOffset now, CancellationToken ct)
    {
        var tokenHash = RefreshTokenGenerator.HashToken(rawRefreshToken);
        var token = await tokenRepo.GetByHashAsync(tokenHash, ct)
            ?? throw new UnauthorizedException("Invalid refresh token.");

        await EnsureTokenNotCompromisedAsync(token, now, ct);

        if (token.IsExpired(now))
        {
            throw new UnauthorizedException("Refresh token has expired.");
        }

        return token;
    }

    private async Task EnsureTokenNotCompromisedAsync(RefreshToken token, DateTimeOffset now, CancellationToken ct)
    {
        if (token.IsRevoked && !token.IsWithinReuseLeeway(now, AuthConstants.RefreshTokenReuseLeewaySeconds))
        {
            await tokenRepo.RevokeAllByUserIdAsync(token.UserId, now, ct);
            throw new TokenReuseCompromiseException(token.UserId);
        }
    }

    private async Task<User> ValidateUserAsync(UserId userId, CancellationToken ct)
    {
        var user = await userRepo.GetByIdAsync(userId, ct);
        if (user == null || !user.IsActive)
        {
            throw new UnauthorizedException("User account is inactive or not found.");
        }

        return user;
    }

    private async Task<string> CreateReplacementTokenAsync(
        UserId userId,
        RefreshToken token,
        DateTimeOffset now,
        CancellationToken ct)
    {
        var rawNew = RefreshTokenGenerator.GenerateTokenString();
        var newHash = RefreshTokenGenerator.HashToken(rawNew);

        if (!token.IsRevoked)
        {
            token.Revoke(now, newHash);
        }

        var newToken = RefreshToken.Create(userId, newHash, now.AddDays(AuthConstants.RefreshTokenLifetimeDays), now);
        await tokenRepo.AddAsync(newToken, ct);
        return rawNew;
    }
}

