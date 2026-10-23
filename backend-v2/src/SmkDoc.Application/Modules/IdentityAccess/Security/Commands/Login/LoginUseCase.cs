using FluentValidation;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.IdentityAccess.Security.DTOs;
using SmkDoc.Application.Modules.IdentityAccess.Security.Helpers;
using SmkDoc.Domain.Constants;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Enums;
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
    IValidator<LoginCommand> validator,
    IRefreshTokenRepository tokenRepo,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider,
    IExecutionContext executionContext,
    ISecurityAuditService auditService) : IUseCase<LoginCommand, LoginResultDto>
{
    public async Task<LoginResultDto> ExecuteAsync(LoginCommand request, CancellationToken ct = default)
    {
        var validationResult = await validator.ValidateAsync(request, ct);
        if (!validationResult.IsValid)
        {
            throw new ValidationException(validationResult.ToDictionary());
        }

        var email = EmailAddress.Create(request.Email);
        var user = await userRepo.GetByEmailAsync(email, ct);

        // Constant-time execution defense against user enumeration timing attacks (OWASP)
        if (user == null)
        {
            passwordHasher.VerifyPassword(request.Password, SecurityConstants.DummyPasswordHash);
            auditService.LogLoginFailure(request.Email, "UserNotFound");
            throw new UnauthorizedException("Invalid email or password.");
        }

        if (!user.IsActive)
        {
            passwordHasher.VerifyPassword(request.Password, SecurityConstants.DummyPasswordHash);
            auditService.LogLoginFailure(request.Email, "UserInactive");
            throw new UnauthorizedException("Invalid email or password.");
        }

        if (!passwordHasher.VerifyPassword(request.Password, user.PasswordHash))
        {
            auditService.LogLoginFailure(request.Email, "InvalidPassword");
            throw new UnauthorizedException("Invalid email or password.");
        }

        var workspace = await workspaceQueryService.ResolveWorkspaceContextAsync(
            user,
            executionContext.ProjectId,
            throwOnUnauthorized: true,
            ct);

        var roles = new List<string> { workspace.ActiveRole };
        var token = jwtTokenGenerator.GenerateToken(user, workspace.ActiveProjectId, roles);

        var now = timeProvider.GetUtcNow();
        var rawRefreshToken = RefreshTokenHelper.GenerateTokenString();
        var refreshTokenHash = RefreshTokenHelper.HashToken(rawRefreshToken);
        var refreshToken = RefreshTokenEntity.Create(user.Id, refreshTokenHash, now.AddDays(SecurityConstants.RefreshTokenLifetimeDays), now);
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
