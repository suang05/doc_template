using FluentValidation;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.IdentityAccess.Security.DTOs;
using SmkDoc.Application.Modules.IdentityAccess.Security.Helpers;
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
    IValidator<LoginCommand>? validator = null,
    IRefreshTokenRepository? tokenRepo = null,
    IUnitOfWork? unitOfWork = null,
    TimeProvider? timeProvider = null,
    IExecutionContext? executionContext = null) : IUseCase<LoginCommand, LoginResultDto>
{
    private readonly TimeProvider _timeProvider = timeProvider ?? TimeProvider.System;

    public async Task<LoginResultDto> ExecuteAsync(LoginCommand request, CancellationToken ct = default)
    {
        if (validator != null)
        {
            var validationResult = await validator.ValidateAsync(request, ct);
            if (!validationResult.IsValid)
            {
                throw new ValidationException(validationResult.ToDictionary());
            }
        }

        var email = EmailAddress.Create(request.Email);
        var user = await userRepo.GetByEmailAsync(email, ct);

        if (user == null || !user.IsActive || !passwordHasher.VerifyPassword(request.Password, user.PasswordHash))
        {
            throw new UnauthorizedException("Invalid email or password.");
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
        var token = jwtTokenGenerator.GenerateToken(user, activeProjectId, roles);

        string? rawRefreshToken = null;
        if (tokenRepo != null && unitOfWork != null)
        {
            var now = _timeProvider.GetUtcNow();
            rawRefreshToken = RefreshTokenHelper.GenerateTokenString();
            var refreshTokenHash = RefreshTokenHelper.HashToken(rawRefreshToken);
            var refreshToken = RefreshTokenEntity.Create(user.Id, refreshTokenHash, now.AddDays(7), now);
            await tokenRepo.AddAsync(refreshToken, ct);
            await unitOfWork.CommitAsync(ct);
        }

        var activeApiKeys = activeProjectId.HasValue
            ? await workspaceQueryService.GetActiveApiKeysAsync(activeProjectId.Value, ct)
            : [];

        return new LoginResultDto
        {
            AccessToken = token,
            RefreshToken = rawRefreshToken,
            User = new UserProfileDto(user.Id, user.Email.Value, user.FirstName, user.LastName, user.SystemRole.Name),
            AccessibleProjects = accessibleProjects,
            ActiveApiKeys = activeApiKeys,
            DefaultProjectId = activeProjectId
        };
    }
}
