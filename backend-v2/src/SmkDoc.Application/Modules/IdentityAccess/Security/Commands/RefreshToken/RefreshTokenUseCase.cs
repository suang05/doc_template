using FluentValidation;
using SmkDoc.Application.Common.Constants;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.IdentityAccess.Security.DTOs;
using SmkDoc.Application.Modules.IdentityAccess.Security.Helpers;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Enums;
using SmkDoc.Domain.Exceptions;
using SmkDoc.Domain.Interfaces;
using RefreshTokenEntity = SmkDoc.Domain.Entities.RefreshToken;

namespace SmkDoc.Application.Modules.IdentityAccess.Security.Commands.RefreshToken;

public sealed class RefreshTokenUseCase(
    IRefreshTokenRepository tokenRepo,
    IUserRepository userRepo,
    IJwtTokenGenerator jwtTokenGenerator,
    IUserWorkspaceQueryService workspaceQueryService,
    IUnitOfWork unitOfWork,
    IValidator<RefreshTokenCommand> validator,
    TimeProvider timeProvider,
    IExecutionContext executionContext,
    ISecurityAuditService auditService) : IUseCase<RefreshTokenCommand, TokenResultDto>
{
    public async Task<TokenResultDto> ExecuteAsync(RefreshTokenCommand request, CancellationToken ct = default)
    {
        var validationResult = await validator.ValidateAsync(request, ct);
        if (!validationResult.IsValid)
        {
            throw new ValidationException(validationResult.ToDictionary());
        }

        var now = timeProvider.GetUtcNow();
        var tokenHash = RefreshTokenHelper.HashToken(request.RefreshToken);
        var token = await tokenRepo.GetByHashAsync(tokenHash, ct);

        if (token == null)
        {
            throw new UnauthorizedException("Invalid refresh token.");
        }

        if (token.IsRevoked)
        {
            if (!token.IsWithinReuseLeeway(now, AuthConstants.RefreshTokenReuseLeewaySeconds))
            {
                // Compromise detected beyond grace window: revoke all active sessions for this user
                auditService.LogTokenReuseDetected(token.UserId.Value);
                await tokenRepo.RevokeAllByUserIdAsync(token.UserId, now, ct);
                await unitOfWork.CommitAsync(ct);
                throw new UnauthorizedException("Invalid refresh token (reuse detected).");
            }
        }

        if (token.IsExpired(now))
        {
            throw new UnauthorizedException("Refresh token has expired.");
        }

        var user = await userRepo.GetByIdAsync(token.UserId, ct);
        if (user == null || !user.IsActive)
        {
            throw new UnauthorizedException("User account is inactive or not found.");
        }

        var workspace = await workspaceQueryService.ResolveWorkspaceContextAsync(
            user,
            executionContext.ProjectId,
            throwOnUnauthorized: true,
            ct);

        var roles = new List<string> { workspace.ActiveRole };
        var newAccessToken = jwtTokenGenerator.GenerateToken(user, workspace.ActiveProjectId, roles);

        // Token Rotation (RTR)
        var newRawToken = RefreshTokenHelper.GenerateTokenString();
        var newTokenHash = RefreshTokenHelper.HashToken(newRawToken);

        if (!token.IsRevoked)
        {
            token.Revoke(now, newTokenHash);
        }

        var newRefreshToken = RefreshTokenEntity.Create(
            user.Id,
            newTokenHash,
            now.AddDays(AuthConstants.RefreshTokenLifetimeDays),
            now);

        await tokenRepo.AddAsync(newRefreshToken, ct);

        // Atomic Unit of Work commit
        await unitOfWork.CommitAsync(ct);

        auditService.LogTokenRotated(user.Id.Value);

        return new TokenResultDto(
            newAccessToken,
            newRawToken,
            AuthConstants.AccessTokenLifetimeSeconds,
            AuthConstants.BearerTokenType);
    }
}
