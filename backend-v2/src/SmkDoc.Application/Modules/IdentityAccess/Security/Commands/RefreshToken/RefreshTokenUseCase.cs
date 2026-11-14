using SmkDoc.Application.Common.Constants;
using SmkDoc.Application.Modules.IdentityAccess.Security.DTOs;
using SmkDoc.Application.Modules.IdentityAccess.Security.Services;
using SmkDoc.Domain.ValueObjects.Ids;

namespace SmkDoc.Application.Modules.IdentityAccess.Security.Commands.RefreshToken;

/// <summary>
/// Executes refresh token rotation (RTR) via <see cref="ITokenSessionGenerator"/>.
/// Guarantees atomic session renewal and immediate defense against token reuse hijacking.
/// </summary>
public sealed class RefreshTokenUseCase(
    ITokenSessionGenerator tokenSessionGenerator,
    IUnitOfWork unitOfWork,
    ISecurityAuditService auditService) : IUseCase<RefreshTokenCommand, TokenResultDto>
{
    public async Task<TokenResultDto> ExecuteAsync(RefreshTokenCommand command, CancellationToken ct)
    {
        try
        {
            var rotated = await tokenSessionGenerator.RotateTokensAsync(command.RefreshToken, ct: ct);
            await unitOfWork.CommitAsync(ct);
            auditService.LogTokenRotated(rotated.UserId.Value);

            return new TokenResultDto(
                rotated.AccessToken,
                rotated.NewRefreshToken,
                AuthConstants.AccessTokenLifetimeSeconds,
                AuthConstants.BearerTokenType);
        }
        catch (TokenReuseCompromiseException ex)
        {
            await HandleCompromiseAsync(ex.UserId, ct);
            throw new UnauthorizedException("Invalid refresh token (reuse detected).");
        }
    }

    private async Task HandleCompromiseAsync(UserId userId, CancellationToken ct)
    {
        auditService.LogTokenReuseDetected(userId.Value);
        await unitOfWork.CommitAsync(ct);
    }
}
