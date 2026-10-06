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
    private readonly IProjectRepository _projectRepo = projectRepo;
    private readonly IUserProjectRoleRepository _userRoleRepo = userRoleRepo;
    private readonly ICompanyRepository _companyRepo = companyRepo;
    private readonly IUnitOfWork _unitOfWork = unitOfWork;
    private readonly IValidator<CreateProjectCommand>? _validator = validator;
    private readonly TimeProvider _timeProvider = timeProvider ?? TimeProvider.System;

    public async Task<ProjectResultDto> ExecuteAsync(CreateProjectCommand request, CancellationToken ct = default)
    {
        if (_validator != null)
        {
            var validationResult = await _validator.ValidateAsync(request, ct);
            if (!validationResult.IsValid)
            {
                throw new ValidationException(validationResult.ToDictionary());
            }
        }

        var existing = await _projectRepo.GetBySlugAsync(request.Slug, ct);
        if (existing != null)
        {
            throw new ConflictException($"Project with slug '{request.Slug}' already exists.");
        }

        var now = _timeProvider.GetUtcNow();

        var defaultCompany = await _companyRepo.GetFirstAsync(ct);
        if (defaultCompany == null)
        {
            defaultCompany = Company.Create(CompanyName.Create("Default Company"), now);
            await _companyRepo.AddAsync(defaultCompany, ct);
            await _unitOfWork.CommitAsync(ct);
        }

        var project = Project.Create(defaultCompany.Id, ProjectName.Create(request.Name), TemplateSlug.Create(request.Slug), now);
        await _projectRepo.AddAsync(project, ct);

        var role = UserProjectRole.Create(request.UserId, project.Id, RoleType.Admin, now);
        await _userRoleRepo.AddAsync(role, ct);

        await _unitOfWork.CommitAsync(ct);

        return new ProjectResultDto(project.Id, project.Name, project.Slug, project.IsActive, project.CreatedAt);
    }
}
