using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.Integration.Datasets.DTOs;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Interfaces;

namespace SmkDoc.Application.Modules.Integration.Datasets.Queries.ListDatasets;

public record ListDatasetsQuery;

public sealed class ListDatasetsUseCase(
    IDatasetRepository datasetRepo,
    IDataConnectionRepository connectionRepo) : IUseCase<ListDatasetsQuery, List<DatasetDto>>
{
    private readonly IDatasetRepository _datasetRepo = datasetRepo;
    private readonly IDataConnectionRepository _connectionRepo = connectionRepo;

    public async Task<List<DatasetDto>> ExecuteAsync(ListDatasetsQuery query, CancellationToken ct = default)
    {
        var datasets = await _datasetRepo.ListAsync(ct);
        var missingConnectionIds = datasets
            .Where(d => d.DataConnection == null)
            .Select(d => d.DataConnectionId)
            .Distinct()
            .ToList();

        Dictionary<Guid, DataConnection> fallbackConns = [];
        if (missingConnectionIds.Count > 0)
        {
            var loaded = await _connectionRepo.GetByIdsAsync(missingConnectionIds, ct);
            fallbackConns = loaded.ToDictionary(c => c.Id);
        }

        return datasets.Select(d =>
        {
            var connName = d.DataConnection?.Name
                ?? (fallbackConns.TryGetValue(d.DataConnectionId, out var c) ? c.Name : string.Empty);

            return new DatasetDto
            {
                Id = d.Id,
                Name = d.Name,
                Description = d.Description,
                DataConnectionId = d.DataConnectionId,
                DataConnectionName = connName,
                SqlQuery = d.SqlQuery,
                CacheSeconds = d.CacheSeconds,
                CreatedAt = d.CreatedAt,
                UpdatedAt = d.UpdatedAt
            };
        }).ToList();
    }
}
