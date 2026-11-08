using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.Authoring.FieldMappings.DTOs;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Exceptions;
using SmkDoc.Domain.Interfaces;
using SmkDoc.Domain.ValueObjects;
using SmkDoc.Domain.ValueObjects.Ids;

namespace SmkDoc.Application.Modules.Authoring.FieldMappings.Commands.SaveTemplateDatasets;

public sealed class SaveTemplateDatasetsUseCase(
    ITemplateRepository templateRepo,
    IDatasetRepository datasetRepo,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : IUseCase<SaveTemplateDatasetsCommand, List<TemplateDatasetDto>>
{
    public async Task<List<TemplateDatasetDto>> ExecuteAsync(SaveTemplateDatasetsCommand command, CancellationToken ct = default)
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

        var datasetIds = datasets.Select(r => r.DatasetId).Distinct().ToList();
        var dsEntities = await datasetRepo.GetByIdsAsync(datasetIds, ct);
        var dsMap = dsEntities.ToDictionary(d => d.Id, d => d.Name.Value);

        return datasets
            .OrderBy(r => r.SortOrder)
            .Select(r => new TemplateDatasetDto(
                r.Id,
                r.TemplateId.Value,
                r.DatasetId.Value,
                dsMap.GetValueOrDefault(r.DatasetId, "(unknown)"),
                r.Alias.Value,
                r.SortOrder))
            .ToList();
    }
}
