using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.Authoring.FieldMappings.DTOs;
using SmkDoc.Domain.Interfaces;

namespace SmkDoc.Application.Modules.Authoring.FieldMappings.Queries.GetTemplateDatasets;

public record GetTemplateDatasetsQuery(Guid TemplateId);

public sealed class GetTemplateDatasetsUseCase(
    ITemplateRepository templateRepo,
    IDatasetRepository datasetRepo) : IUseCase<GetTemplateDatasetsQuery, List<TemplateDatasetDto>>
{
    private readonly ITemplateRepository _templateRepo = templateRepo;
    private readonly IDatasetRepository _datasetRepo = datasetRepo;

    public async Task<List<TemplateDatasetDto>> ExecuteAsync(GetTemplateDatasetsQuery query, CancellationToken ct = default)
    {
        var template = await _templateRepo.GetByIdWithDetailsAsync(query.TemplateId, ct);
        if (template == null) return [];

        var rows = template.TemplateDatasets;
        var datasetIds = rows.Select(r => r.DatasetId).Distinct().ToList();
        var datasets = await _datasetRepo.GetByIdsAsync(datasetIds, ct);
        var dsMap = datasets.ToDictionary(d => d.Id, d => d.Name);

        return rows
            .OrderBy(r => r.SortOrder)
            .Select(r => new TemplateDatasetDto(
                r.Id,
                r.TemplateId,
                r.DatasetId,
                dsMap.GetValueOrDefault(r.DatasetId, "(unknown)"),
                r.Alias,
                r.SortOrder))
            .ToList();
    }
}
