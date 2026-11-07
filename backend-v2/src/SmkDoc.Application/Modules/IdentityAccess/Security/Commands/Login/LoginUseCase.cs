using SmkDoc.Application.Common.Constants;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.IdentityAccess.Security.DTOs;
using SmkDoc.Application.Modules.IdentityAccess.Security.Helpers;
using SmkDoc.Domain.Exceptions;
using SmkDoc.Domain.Interfaces;
using SmkDoc.Domain.ValueObjects;
using RefreshTokenEntity = SmkDoc.Domain.Entities.RefreshToken;

namespace SmkDoc.Application.Modules.IdentityAccess.Security.Commands.Login;

public sealed class LoginUseCase(
    IUserRepository userRepo,
    IPasswordHasher passwordHasher,
    IJwtTokenGenerator jwtTokenGenerator,
    IUserWorkspaceQueryService workspaceQueryService,
    IRefreshTokenRepository tokenRepo,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider,
    ICurrentProjectContext projectContext,
    ISecurityAuditService auditService) : IUseCase<LoginCommand, LoginResultDto>
{
    public async Task<LoginResultDto> ExecuteAsync(LoginCommand command, CancellationToken ct = default)
    {
        var email = EmailAddress.Create(command.Email);
        var user = await userRepo.GetByEmailAsync(email, ct);

        // Constant-time execution defense against user enumeration timing attacks (OWASP)
        if (user == null)
        {
            passwordHasher.VerifyPassword(command.Password, AuthConstants.DummyPasswordHash);
            auditService.LogLoginFailure(command.Email, "UserNotFound");
            throw new UnauthorizedException("Invalid email or password.");
        }

        if (!user.IsActive)
        {
            passwordHasher.VerifyPassword(command.Password, AuthConstants.DummyPasswordHash);
            auditService.LogLoginFailure(command.Email, "UserInactive");
            throw new UnauthorizedException("Invalid email or password.");
        }

        if (!passwordHasher.VerifyPassword(command.Password, user.PasswordHash))
        {
            auditService.LogLoginFailure(command.Email, "InvalidPassword");
            throw new UnauthorizedException("Invalid email or password.");
        }

        var workspace = await workspaceQueryService.ResolveWorkspaceContextAsync(
            user,
            projectContext.IsResolved ? projectContext.ProjectId.Value : (Guid?)null,
            throwOnUnauthorized: true,
            ct);

        var roles = new List<string> { workspace.ActiveRole };
        var token = jwtTokenGenerator.GenerateToken(user, workspace.ActiveProjectId, roles);

        var now = timeProvider.GetUtcNow();
        var rawRefreshToken = RefreshTokenGenerator.GenerateTokenString();
        var refreshTokenHash = RefreshTokenGenerator.HashToken(rawRefreshToken);
        var refreshToken = RefreshTokenEntity.Create(user.Id, refreshTokenHash, now.AddDays(AuthConstants.RefreshTokenLifetimeDays), now);
        await tokenRepo.AddAsync(refreshToken, ct);
        await unitOfWork.CommitAsync(ct);

        var activeApiKeys = workspace.ActiveProjectId.HasValue
            ? await workspaceQueryService.GetActiveApiKeysAsync(workspace.ActiveProjectId.Value, ct)
            : [];

        auditService.LogLoginSuccess(user.Id.Value, user.Email.Value, workspace.ActiveProjectId);

        return new LoginResultDto(
            accessToken: token,
            user: new UserProfileDto(user.Id.Value, user.Email.Value, user.FirstName, user.LastName, user.SystemRole.Name),
            accessibleProjects: workspace.AccessibleProjects,
            defaultProjectId: workspace.DefaultProjectId,
            activeProjectId: workspace.ActiveProjectId,
            activeRole: workspace.ActiveRole,
            refreshToken: rawRefreshToken,
            activeApiKeys: activeApiKeys);
    }
}
