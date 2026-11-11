using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.Authoring.FieldMappings.DTOs;
using SmkDoc.Domain.Interfaces;
using SmkDoc.Domain.ValueObjects.Ids;

namespace SmkDoc.Application.Modules.Authoring.FieldMappings.Queries.GetTemplateDatasets;

public sealed class GetTemplateDatasetsUseCase(
    ITemplateRepository templateRepo,
    IDatasetRepository datasetRepo) : IUseCase<GetTemplateDatasetsQuery, List<TemplateDatasetResultDto>>
{
    public async Task<List<TemplateDatasetResultDto>> ExecuteAsync(GetTemplateDatasetsQuery query, CancellationToken ct = default)
    {
        var template = await templateRepo.GetByIdWithDetailsAsync(new TemplateId(query.TemplateId), ct);
        if (template == null) return [];

        var rows = template.TemplateDatasets;
        var datasetIds = rows.Select(r => r.DatasetId).Distinct().ToList();
        var datasets = await datasetRepo.GetByIdsAsync(datasetIds, ct);
        var dsMap = datasets.ToDictionary(d => d.Id, d => d.Name.Value);

        return rows
            .OrderBy(r => r.SortOrder)
            .Select(r => new TemplateDatasetResultDto(
                r.Id,
                r.TemplateId.Value,
                r.DatasetId.Value,
                dsMap.GetValueOrDefault(r.DatasetId, "(unknown)"),
                r.Alias.Value,
                r.SortOrder))
            .ToList();
    }
}
