using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.Authoring.Templates.DTOs;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Exceptions;
using SmkDoc.Domain.Interfaces;

namespace SmkDoc.Application.Modules.Authoring.Templates.Commands.ActivateTemplateVersion;

/// <summary>
/// Single-responsibility Use Case for activating a specific template version.
/// Enforces domain invariant encapsulation via template.SetCurrentVersion and template.Activate.
/// </summary>
public sealed class ActivateTemplateVersionUseCase(
    ITemplateRepository templateRepo,
    IRepository<TemplateVersion> versionRepo,
    IUnitOfWork unitOfWork) : IUseCase<ActivateTemplateVersionCommand, TemplateResponse>
{
    private readonly ITemplateRepository _templateRepo = templateRepo;
    private readonly IRepository<TemplateVersion> _versionRepo = versionRepo;
    private readonly IUnitOfWork _unitOfWork = unitOfWork;

    public async Task<TemplateResponse> ExecuteAsync(ActivateTemplateVersionCommand command, CancellationToken ct = default)
    {
        // 1. Fetch Aggregate Root
        var template = await _templateRepo.GetByIdAsync(command.TemplateId, ct)
            ?? throw new NotFoundException($"Template '{command.TemplateId}' was not found.");

        // 2. Fetch & Validate Version
        var version = await _versionRepo.GetByIdAsync(command.VersionId, ct)
            ?? throw new NotFoundException($"TemplateVersion '{command.VersionId}' was not found.");

        if (version.TemplateId != template.Id)
        {
            throw new ConflictException($"Version '{command.VersionId}' does not belong to Template '{command.TemplateId}'.");
        }

        // 3. Domain Invariants Execution
        template.SetCurrentVersion(version.Id);
        template.Activate();

        // 4. Persistence & Atomic Commit
        _templateRepo.Update(template);
        await _unitOfWork.CommitAsync(ct);

        // 5. Return Safe DTO
        return new TemplateResponse(
            template.Id,
            template.ProjectId,
            template.Name,
            template.Slug,
            template.Category,
            template.IsActive,
            template.CurrentVersionId,
            version.FileFormat?.Name,
            template.CreatedAt,
            template.UpdatedAt
        );
    }
}
