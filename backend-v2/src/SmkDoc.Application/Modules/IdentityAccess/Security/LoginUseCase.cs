using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.IdentityAccess.Security.DTOs;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Enums;

namespace SmkDoc.Application.Modules.IdentityAccess.Security;

public sealed class LoginUseCase(
    IRepository<User> userRepo,
    IRepository<UserProjectRole> roleRepo,
    IRepository<Project> projectRepo,
    IPasswordHasher passwordHasher,
    IJwtTokenGenerator jwtTokenGenerator)
{
    private readonly IRepository<User> _userRepo = userRepo;
    private readonly IRepository<UserProjectRole> _roleRepo = roleRepo;
    private readonly IRepository<Project> _projectRepo = projectRepo;
    private readonly IPasswordHasher _passwordHasher = passwordHasher;
    private readonly IJwtTokenGenerator _jwtTokenGenerator = jwtTokenGenerator;

    public async Task<LoginResponse> ExecuteAsync(LoginCommand request, CancellationToken ct = default)
    {
        var user = await _userRepo.FirstOrDefaultAsync(u => u.Email == request.Email && u.IsActive, ct);

        if (user == null || !_passwordHasher.VerifyPassword(request.Password, user.PasswordHash))
        {
            throw new UnauthorizedAccessException("Invalid email or password.");
        }

        var accessibleProjects = new List<AccessibleProjectDto>();
        string activeRole = "Viewer";

        if (user.SystemRole == SystemRole.SuperAdmin)
        {
            var allProjects = await _projectRepo.ListAsync(p => p.IsActive, ct);
            accessibleProjects = allProjects.Select(p => new AccessibleProjectDto(p.Id, p.Name, p.Slug, "Admin")).ToList();
            activeRole = "Admin";
        }
        else
        {
            var userRoles = await _roleRepo.ListAsync(r => r.UserId == user.Id, ct);
            if (userRoles.Count > 0)
            {
                var projectIds = userRoles.Select(r => r.ProjectId).ToHashSet();
                var projects = await _projectRepo.ListAsync(p => projectIds.Contains(p.Id) && p.IsActive, ct);
                var roleMap = userRoles.ToDictionary(r => r.ProjectId, r => r.Role.ToString());

                accessibleProjects = projects.Select(p => 
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
                throw new UnauthorizedAccessException("User does not have access to the specified project.");
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

