using FluentValidation;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.IdentityAccess.Projects.DTOs;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Enums;
using SmkDoc.Domain.Exceptions;
using SmkDoc.Domain.Interfaces;
using SmkDoc.Domain.ValueObjects;

namespace SmkDoc.Application.Modules.IdentityAccess.Projects.Commands.CreateProject;

public sealed class CreateProjectUseCase(
    IProjectRepository projectRepo,
    IUserProjectRoleRepository userRoleRepo,
    ICompanyRepository companyRepo,
    IUnitOfWork unitOfWork,
    IValidator<CreateProjectCommand>? validator = null,
    TimeProvider? timeProvider = null) : IUseCase<CreateProjectCommand, ProjectResultDto>
{
    private readonly TimeProvider _timeProvider = timeProvider ?? TimeProvider.System;

    public async Task<ProjectResultDto> ExecuteAsync(CreateProjectCommand request, CancellationToken ct = default)
    {
        if (validator != null)
        {
            var validationResult = await validator.ValidateAsync(request, ct);
            if (!validationResult.IsValid)
            {
                throw new ValidationException(validationResult.ToDictionary());
            }
        }

        var existing = await projectRepo.GetBySlugAsync(request.Slug, ct);
        if (existing != null)
        {
            throw new ConflictException($"Project with slug '{request.Slug}' already exists.");
        }

        var now = _timeProvider.GetUtcNow();

        var defaultCompany = await companyRepo.GetFirstAsync(ct);
        if (defaultCompany == null)
        {
            defaultCompany = Company.Create(CompanyName.Create("Default Company"), now);
            await companyRepo.AddAsync(defaultCompany, ct);
        }

        var project = Project.Create(defaultCompany.Id, ProjectName.Create(request.Name), TemplateSlug.Create(request.Slug), now);
        await projectRepo.AddAsync(project, ct);

        var role = UserProjectRole.Create(request.UserId, project.Id, RoleType.Admin, now);
        await userRoleRepo.AddAsync(role, ct);

        // Single atomic transaction boundary
        await unitOfWork.CommitAsync(ct);

        return new ProjectResultDto(project.Id, project.Name, project.Slug, project.IsActive, project.CreatedAt);
    }
}
