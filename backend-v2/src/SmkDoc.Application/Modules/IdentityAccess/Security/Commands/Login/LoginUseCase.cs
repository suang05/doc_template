using FluentValidation;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.IdentityAccess.Security.DTOs;
using SmkDoc.Application.Modules.IdentityAccess.Security.Helpers;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Enums;
using SmkDoc.Domain.Exceptions;
using SmkDoc.Domain.Interfaces;
using RefreshTokenEntity = SmkDoc.Domain.Entities.RefreshToken;

namespace SmkDoc.Application.Modules.IdentityAccess.Security.Commands.Login;

public sealed class LoginUseCase(
    IUserRepository userRepo,
    IUserProjectRoleRepository roleRepo,
    IProjectRepository projectRepo,
    IPasswordHasher passwordHasher,
    IJwtTokenGenerator jwtTokenGenerator,
    IValidator<LoginCommand>? validator = null,
    IRefreshTokenRepository? tokenRepo = null,
    IUnitOfWork? unitOfWork = null,
    TimeProvider? timeProvider = null) : IUseCase<LoginCommand, LoginResultDto>
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

        var normalizedEmail = request.Email.Trim().ToLowerInvariant();
        var user = await userRepo.GetByEmailAsync(normalizedEmail, ct);

        if (user == null || !user.IsActive || !passwordHasher.VerifyPassword(request.Password, user.PasswordHash))
        {
            throw new UnauthorizedException("Invalid email or password.");
        }

        var accessibleProjects = new List<AccessibleProjectDto>();
        string activeRole = "Viewer";

        if (user.SystemRole == SystemRole.SuperAdmin)
        {
            var allProjects = await projectRepo.ListActiveAsync(ct);
            accessibleProjects = allProjects.Select(p => new AccessibleProjectDto(p.Id, p.Name, p.Slug, "Admin")).ToList();
            activeRole = "Admin";
        }
        else
        {
            var userRoles = await roleRepo.ListByUserAsync(user.Id, ct);
            if (userRoles.Count > 0)
            {
                var projectIds = userRoles.Select(r => r.ProjectId).ToHashSet();
                var projects = await projectRepo.ListByIdsAsync(projectIds, ct);
                var activeProjects = projects.Where(p => p.IsActive).ToList();
                var roleMap = userRoles.ToDictionary(r => r.ProjectId, r => r.Role.ToString());

                accessibleProjects = activeProjects.Select(p =>
                    new AccessibleProjectDto(p.Id, p.Name, p.Slug, roleMap.GetValueOrDefault(p.Id, "Viewer"))
                ).ToList();
            }
        }

        Guid? activeProjectId = null;
        if (request.ProjectId.HasValue && request.ProjectId.Value != Guid.Empty)
        {
            var target = accessibleProjects.FirstOrDefault(p => p.Id == request.ProjectId.Value);
            if (target == null && user.SystemRole != SystemRole.SuperAdmin)
            {
                throw new UnauthorizedException("User does not have access to the specified project.");
            }
            activeProjectId = request.ProjectId.Value;
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

        return new LoginResultDto
        {
            AccessToken = token,
            RefreshToken = rawRefreshToken,
            User = new UserProfileDto(user.Id, user.Email, user.FirstName, user.LastName, user.SystemRole.Name),
            AccessibleProjects = accessibleProjects,
            DefaultProjectId = activeProjectId
        };
    }
}
