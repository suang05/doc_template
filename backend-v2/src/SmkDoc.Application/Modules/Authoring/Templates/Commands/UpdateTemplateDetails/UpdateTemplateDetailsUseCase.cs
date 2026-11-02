using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.Authoring.Templates.DTOs;
using SmkDoc.Domain.Exceptions;
using SmkDoc.Domain.Interfaces;
using SmkDoc.Domain.ValueObjects;
using SmkDoc.Domain.ValueObjects.Ids;

namespace SmkDoc.Application.Modules.Authoring.Templates.Commands.UpdateTemplateDetails;

/// <summary>
/// Single-responsibility Use Case for updating template metadata (name, category).
/// Enforces domain encapsulation through entity method calls.
/// </summary>
public sealed class UpdateTemplateDetailsUseCase(
    ITemplateRepository templateRepo,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : IUseCase<UpdateTemplateDetailsCommand, TemplateResultDto>
{
    public async Task<TemplateResultDto> ExecuteAsync(UpdateTemplateDetailsCommand command, CancellationToken ct = default)
    {
        // 1. Fetch Aggregate Root
        var template = await templateRepo.GetByIdAsync(new TemplateId(command.TemplateId), ct)
            ?? throw new NotFoundException($"Template '{command.TemplateId}' was not found.");

        // 2. Domain Invariants Execution
        var now = timeProvider.GetUtcNow();
        template.UpdateDetails(TemplateName.Create(command.Name), command.Category?.Trim(), now);

        // 3. Persistence & Atomic Commit
        templateRepo.Update(template);
        await unitOfWork.CommitAsync(ct);

        // 4. Return Safe DTO
        return new TemplateResultDto(
            template.Id.Value,
            template.ProjectId.Value,
            template.Name.Value,
            template.Slug.Value,
            template.Category,
            template.IsActive,
            template.CurrentVersionId,
            template.CurrentVersion?.FileFormat?.Name,
            template.CreatedAt,
            template.UpdatedAt
        );
    }
}
