using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.Integration.Datasets.DTOs;
using SmkDoc.Domain.Interfaces;

namespace SmkDoc.Application.Modules.Integration.Datasets.Queries.GetDatasetById;

public record GetDatasetByIdQuery(Guid Id);

public sealed class GetDatasetByIdUseCase(
    IDatasetRepository datasetRepo,
    IDataConnectionRepository connectionRepo) : IUseCase<GetDatasetByIdQuery, DatasetDto?>
{
    private readonly IDatasetRepository _datasetRepo = datasetRepo;
    private readonly IDataConnectionRepository _connectionRepo = connectionRepo;

    public async Task<DatasetDto?> ExecuteAsync(GetDatasetByIdQuery query, CancellationToken ct = default)
    {
        var dataset = await _datasetRepo.GetByIdAsync(query.Id, ct);
        if (dataset == null) return null;

        var connection = dataset.DataConnection ?? await _connectionRepo.GetByIdAsync(dataset.DataConnectionId, ct);

        return new DatasetDto
        {
            Id = dataset.Id,
            Name = dataset.Name,
            Description = dataset.Description,
            DataConnectionId = dataset.DataConnectionId,
            DataConnectionName = connection?.Name ?? string.Empty,
            SqlQuery = dataset.SqlQuery,
            CacheSeconds = dataset.CacheSeconds,
            CreatedAt = dataset.CreatedAt,
            UpdatedAt = dataset.UpdatedAt
        };
    }
}
