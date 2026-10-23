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

            return new DatasetDto
            {
                Id = d.Id.Value,
                Name = d.Name.Value,
                Description = d.Description,
                DataConnectionId = d.DataConnectionId.Value,
                DataConnectionName = connName,
                SqlQuery = d.SqlQuery,
                CacheSeconds = d.CacheSeconds,
                CreatedAt = d.CreatedAt,
                UpdatedAt = d.UpdatedAt
            };
        }).ToList();
    }
}
