using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.Integration.Datasets.DTOs;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Interfaces;
using SmkDoc.Domain.ValueObjects.Ids;

namespace SmkDoc.Application.Modules.Integration.Datasets.Queries.ListDatasets;

public record ListDatasetsQuery;

public sealed class ListDatasetsUseCase(
    IDatasetRepository datasetRepo,
    IDataConnectionRepository connectionRepo) : IUseCase<ListDatasetsQuery, List<DatasetDto>>
{
    public async Task<List<DatasetDto>> ExecuteAsync(ListDatasetsQuery query, CancellationToken ct = default)
    {
        var datasets = await datasetRepo.ListAsync(ct);
        var missingConnectionIds = datasets
            .Where(d => d.DataConnection == null)
            .Select(d => d.DataConnectionId)
            .Distinct()
            .ToList();

        Dictionary<DataConnectionId, DataConnection> fallbackConns = [];
        if (missingConnectionIds.Count > 0)
        {
            var loaded = await connectionRepo.GetByIdsAsync(missingConnectionIds, ct);
            fallbackConns = loaded.ToDictionary(c => c.Id);
        }

        return datasets.Select(d =>
        {
            var connName = d.DataConnection?.Name.Value
                ?? (fallbackConns.TryGetValue(d.DataConnectionId, out var c) ? c.Name.Value : string.Empty);

            return new DatasetDto(
                d.Id.Value,
                d.Name.Value,
                d.Description,
                d.DataConnectionId.Value,
                connName,
                d.SqlQuery,
                d.CacheSeconds,
                d.CreatedAt,
                d.UpdatedAt);
        }).ToList();
    }
}
