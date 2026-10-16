using FluentValidation;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.IdentityAccess.Security.DTOs;
using SmkDoc.Application.Modules.IdentityAccess.Security.Helpers;
using SmkDoc.Domain.Constants;
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
    IValidator<RefreshTokenCommand>? validator = null,
    TimeProvider? timeProvider = null,
    IExecutionContext? executionContext = null) : IUseCase<RefreshTokenCommand, TokenResultDto>
{
    private readonly TimeProvider _timeProvider = timeProvider ?? TimeProvider.System;

    public async Task<TokenResultDto> ExecuteAsync(RefreshTokenCommand request, CancellationToken ct = default)
    {
        if (validator != null)
        {
            var validationResult = await validator.ValidateAsync(request, ct);
            if (!validationResult.IsValid)
            {
                throw new ValidationException(validationResult.ToDictionary());
            }
        }

        var now = _timeProvider.GetUtcNow();
        var tokenHash = RefreshTokenHelper.HashToken(request.RefreshToken);
        var token = await tokenRepo.GetByHashAsync(tokenHash, ct);

        if (token == null)
        {
            throw new UnauthorizedException("Invalid refresh token.");
        }

        if (token.IsRevoked)
        {
            // Reuse Detection: compromise detected, revoke all active sessions for this user
            await tokenRepo.RevokeAllByUserIdAsync(token.UserId, now, ct);
            await unitOfWork.CommitAsync(ct);
            throw new UnauthorizedException("Invalid refresh token (reuse detected).");
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

        var accessibleProjects = await workspaceQueryService.GetAccessibleProjectsAsync(
            user.Id,
            user.SystemRole == SystemRole.SuperAdmin,
            ct);

        string activeRole = user.SystemRole == SystemRole.SuperAdmin ? "Admin" : "Viewer";
        Guid? activeProjectId = executionContext?.ProjectId;

        if (activeProjectId.HasValue && activeProjectId.Value != Guid.Empty)
        {
            var target = accessibleProjects.FirstOrDefault(p => p.Id == activeProjectId.Value);
            if (target == null && user.SystemRole != SystemRole.SuperAdmin)
            {
                throw new UnauthorizedException("User does not have access to the specified project.");
            }

            if (target != null)
            {
                activeRole = target.Role;
            }
        }
        else
        {
            var defaultProject = accessibleProjects.FirstOrDefault();
            activeProjectId = defaultProject?.Id;
            if (defaultProject != null)
            {
                activeRole = defaultProject.Role;
            }
        }

        var roles = new List<string> { activeRole };
        var newAccessToken = jwtTokenGenerator.GenerateToken(user, activeProjectId, roles);

        // Token Rotation (RTR)
        var newRawToken = RefreshTokenHelper.GenerateTokenString();
        var newTokenHash = RefreshTokenHelper.HashToken(newRawToken);

        token.Revoke(now, newTokenHash);

        var newRefreshToken = RefreshTokenEntity.Create(
            user.Id,
            newTokenHash,
            now.AddDays(SecurityConstants.RefreshTokenLifetimeDays),
            now);

        await tokenRepo.AddAsync(newRefreshToken, ct);

        // Atomic Unit of Work commit
        await unitOfWork.CommitAsync(ct);

        return new TokenResultDto(
            newAccessToken,
            newRawToken,
            SecurityConstants.AccessTokenLifetimeSeconds,
            SecurityConstants.BearerTokenType);
    }
}
