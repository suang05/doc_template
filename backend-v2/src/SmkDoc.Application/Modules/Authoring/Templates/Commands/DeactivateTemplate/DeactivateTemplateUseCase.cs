using SmkDoc.Domain.ValueObjects.Ids;

namespace SmkDoc.Application.Modules.Authoring.Templates.Commands.DeactivateTemplate;

/// <summary>
/// Single-responsibility Use Case for deactivating a template.
/// </summary>
public sealed class DeactivateTemplateUseCase(
    ITemplateRepository templateRepo,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : IUseCase<DeactivateTemplateCommand>
{
    public async Task ExecuteAsync(DeactivateTemplateCommand command, CancellationToken ct)
    {
        var template = await templateRepo.GetByIdAsync(new TemplateId(command.TemplateId), ct)
            ?? throw new NotFoundException($"Template '{command.TemplateId}' was not found.");

        var now = timeProvider.GetUtcNow();
        template.Deactivate(now);

        templateRepo.Update(template);
        await unitOfWork.CommitAsync(ct);
    }
}
