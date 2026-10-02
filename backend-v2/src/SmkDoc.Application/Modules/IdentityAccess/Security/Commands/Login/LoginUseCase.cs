using FluentValidation;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.IdentityAccess.Security.DTOs;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Enums;
using SmkDoc.Domain.Exceptions;
using SmkDoc.Domain.Interfaces;

namespace SmkDoc.Application.Modules.IdentityAccess.Security.Commands.Login;

public sealed class LoginUseCase(
    IUserRepository userRepo,
    IUserProjectRoleRepository roleRepo,
    IProjectRepository projectRepo,
    IPasswordHasher passwordHasher,
    IJwtTokenGenerator jwtTokenGenerator,
    IValidator<LoginCommand>? validator = null) : IUseCase<LoginCommand, LoginResponse>
{
    private readonly IUserRepository _userRepo = userRepo;
    private readonly IUserProjectRoleRepository _roleRepo = roleRepo;
    private readonly IProjectRepository _projectRepo = projectRepo;
    private readonly IPasswordHasher _passwordHasher = passwordHasher;
    private readonly IJwtTokenGenerator _jwtTokenGenerator = jwtTokenGenerator;
    private readonly IValidator<LoginCommand>? _validator = validator;

    public async Task<LoginResponse> ExecuteAsync(LoginCommand request, CancellationToken ct = default)
    {
        if (_validator != null)
        {
            var validationResult = await _validator.ValidateAsync(request, ct);
            if (!validationResult.IsValid)
            {
                throw new Domain.Exceptions.ValidationException(validationResult.ToDictionary());
            }
        }

        var normalizedEmail = request.Email.Trim().ToLowerInvariant();
        var user = await _userRepo.GetByEmailAsync(normalizedEmail, ct);

        if (user == null || !user.IsActive || !_passwordHasher.VerifyPassword(request.Password, user.PasswordHash))
        {
            throw new UnauthorizedException("Invalid email or password.");
        }

        var accessibleProjects = new List<AccessibleProjectDto>();
        string activeRole = "Viewer";

        if (user.SystemRole == SystemRole.SuperAdmin)
        {
            var allProjects = await _projectRepo.ListActiveAsync(ct);
            accessibleProjects = allProjects.Select(p => new AccessibleProjectDto(p.Id, p.Name, p.Slug, "Admin")).ToList();
            activeRole = "Admin";
        }
        else
        {
            var userRoles = await _roleRepo.ListByUserAsync(user.Id, ct);
            if (userRoles.Count > 0)
            {
                var projectIds = userRoles.Select(r => r.ProjectId).ToHashSet();
                var projects = await _projectRepo.ListByIdsAsync(projectIds, ct);
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
        var token = _jwtTokenGenerator.GenerateToken(user, activeProjectId, roles);

        return new LoginResponse
        {
            AccessToken = token,
            User = new UserProfileDto(user.Id, user.Email, user.FirstName, user.LastName, user.SystemRole.Name),
            AccessibleProjects = accessibleProjects,
            DefaultProjectId = activeProjectId
        };
    }
}
