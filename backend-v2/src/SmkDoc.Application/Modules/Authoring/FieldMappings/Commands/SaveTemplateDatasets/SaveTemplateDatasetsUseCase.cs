using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Exceptions;
using SmkDoc.Domain.Interfaces;
using SmkDoc.Domain.ValueObjects;
using SmkDoc.Domain.ValueObjects.Ids;

namespace SmkDoc.Application.Modules.Authoring.FieldMappings.Commands.SaveTemplateDatasets;

public sealed class SaveTemplateDatasetsUseCase(
    ITemplateRepository templateRepo,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : IUseCase<SaveTemplateDatasetsCommand>
{
    public async Task ExecuteAsync(SaveTemplateDatasetsCommand command, CancellationToken ct = default)
    {
        var templateId = new TemplateId(command.TemplateId);
        var template = await templateRepo.GetByIdWithDetailsAsync(templateId, ct)
            ?? await templateRepo.GetByIdAsync(templateId, ct)
            ?? throw new NotFoundException($"Template '{command.TemplateId}' not found.");

        var now = timeProvider.GetUtcNow();
        var datasets = command.Items.Select(item =>
            TemplateDataset.Create(template.Id, new DatasetId(item.DatasetId), DatasetAlias.Create(item.Alias), item.SortOrder, now)
        ).ToList();

        template.ReplaceDatasets(datasets, now);
        await unitOfWork.CommitAsync(ct);
    }
}
