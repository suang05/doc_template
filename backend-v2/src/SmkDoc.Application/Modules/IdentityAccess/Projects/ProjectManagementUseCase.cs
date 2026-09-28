using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.IdentityAccess.Projects.DTOs;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Enums;
using SmkDoc.Domain.Interfaces;

namespace SmkDoc.Application.Modules.IdentityAccess.Projects;

public sealed class ProjectManagementUseCase(
    IRepository<Project> projectRepo,
    IRepository<UserProjectRole> userRoleRepo,
    IRepository<Company> companyRepo,
    IUnitOfWork unitOfWork)
{
    private readonly IRepository<Project> _projectRepo = projectRepo;
    private readonly IRepository<UserProjectRole> _userRoleRepo = userRoleRepo;
    private readonly IRepository<Company> _companyRepo = companyRepo;
    private readonly IUnitOfWork _unitOfWork = unitOfWork;

    public async Task<IEnumerable<ProjectResultDto>> ListProjectsAsync(Guid userId, CancellationToken ct)
    {
        // Get all projects where the user has a role
        var roles = await _userRoleRepo.ListAsync(r => r.UserId == userId, ct);
        var projectIds = roles.Select(r => r.ProjectId).ToList();

        if (!projectIds.Any())
        {
            return Enumerable.Empty<ProjectResultDto>();
        }

        var projects = await _projectRepo.ListAsync(p => projectIds.Contains(p.Id) && p.IsActive, ct);
        return projects.Select(p => new ProjectResultDto(p.Id, p.Name, p.Slug, p.IsActive, p.CreatedAt));
    }

    public async Task<ProjectResultDto> CreateProjectAsync(Guid userId, string name, string slug, CancellationToken ct)
    {
        // Check if slug exists
        var existing = await _projectRepo.FirstOrDefaultAsync(p => p.Slug == slug, ct);
        if (existing != null)
        {
            throw new InvalidOperationException($"Project with slug '{slug}' already exists.");
        }

        // For now, attach to the first company (assuming single tenant for now)
        var companies = await _companyRepo.ListAsync(_ => true, ct);
        var defaultCompany = companies.FirstOrDefault();
        if (defaultCompany == null)
        {
            defaultCompany = new Company("Default Company");
            await _companyRepo.AddAsync(defaultCompany, ct);
            await _unitOfWork.CommitAsync(ct);
        }

        var project = new Project(defaultCompany.Id, name, slug);

        await _projectRepo.AddAsync(project, ct);

        // Assign Admin role to the creator
        var role = new UserProjectRole { UserId = userId, ProjectId = project.Id, Role = RoleType.Admin };
        await _userRoleRepo.AddAsync(role, ct);

        await _unitOfWork.CommitAsync(ct);

        return new ProjectResultDto(project.Id, project.Name, project.Slug, project.IsActive, project.CreatedAt);
    }
}
