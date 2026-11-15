using SmkDoc.Application.Modules.Authoring.Templates.DTOs;
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
    public async Task<TemplateResultDto> ExecuteAsync(UpdateTemplateDetailsCommand command, CancellationToken ct)
    {
        // 1. Fetch Aggregate Root
        var template = await templateRepo.GetByIdAsync(new TemplateId(command.TemplateId), ct)
            ?? throw new NotFoundException($"Template '{command.TemplateId}' was not found.");

        if (template.Version != command.ExpectedVersion)
        {
            throw PreconditionFailedException.VersionMismatch();
        }

        // 2. Domain Invariants Execution
        var now = timeProvider.GetUtcNow();
        template.UpdateDetails(TemplateName.Create(command.Name), command.Category?.Trim(), now);

        // 3. Persistence & Atomic Commit
        templateRepo.Update(template);
        await unitOfWork.CommitAsync(ct);

        // 4. Return Safe DTO
        return template.ToResultDto();
    }
}
