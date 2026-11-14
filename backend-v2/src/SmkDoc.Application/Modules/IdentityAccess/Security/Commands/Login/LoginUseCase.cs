using SmkDoc.Application.Common.Constants;
using SmkDoc.Application.Modules.IdentityAccess.Security.DTOs;
using SmkDoc.Application.Modules.IdentityAccess.Security.Services;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.ValueObjects;

namespace SmkDoc.Application.Modules.IdentityAccess.Security.Commands.Login;

/// <summary>
/// Authenticates user credentials and delegates token pair issuance to <see cref="ITokenSessionGenerator"/>.
/// Adheres strictly to linear 4-step PEMR cadence and OWASP timing-attack protections.
/// </summary>
public sealed class LoginUseCase(
    IUserRepository userRepo,
    IPasswordHasher passwordHasher,
    ITokenSessionGenerator tokenSessionGenerator,
    IUnitOfWork unitOfWork,
    ISecurityAuditService auditService) : IUseCase<LoginCommand, LoginResultDto>
{
    public async Task<LoginResultDto> ExecuteAsync(LoginCommand command, CancellationToken ct)
    {
        var email = EmailAddress.Create(command.Email);
        var user = await ValidateCredentialsAsync(email, command.Password, ct);

        var issued = await tokenSessionGenerator.IssueTokensAsync(user, ct: ct);
        await unitOfWork.CommitAsync(ct);

        auditService.LogLoginSuccess(user.Id.Value, user.Email.Value, issued.Workspace.ActiveProjectId);
        return CreateLoginResult(user, issued);
    }

    private async Task<User> ValidateCredentialsAsync(EmailAddress email, string password, CancellationToken ct)
    {
        var user = await userRepo.GetByEmailAsync(email, ct);

        // Constant-time execution defense against user enumeration timing attacks (OWASP)
        if (user == null || !user.IsActive || !passwordHasher.VerifyPassword(password, user.PasswordHash))
        {
            if (user == null || !user.IsActive)
            {
                passwordHasher.VerifyPassword(password, AuthConstants.DummyPasswordHash);
            }

            var reason = "InvalidPassword";
            if (user == null)
            {
                reason = "UserNotFound";
            }
            else if (!user.IsActive)
            {
                reason = "UserInactive";
            }
            auditService.LogLoginFailure(email.Value, reason);
            throw new UnauthorizedException("Invalid email or password.");
        }

        return user;
    }

    private static LoginResultDto CreateLoginResult(User user, IssuedTokensResult issued) =>
        new(
            accessToken: issued.AccessToken,
            user: new UserProfileResultDto(user.Id.Value, user.Email.Value, user.FirstName, user.LastName, user.SystemRole.Name),
            accessibleProjects: issued.Workspace.AccessibleProjects,
            defaultProjectId: issued.Workspace.DefaultProjectId,
            activeProjectId: issued.Workspace.ActiveProjectId,
            activeRole: issued.Workspace.ActiveRole,
            refreshToken: issued.RefreshToken,
            activeApiKeys: issued.ActiveApiKeys);
}
