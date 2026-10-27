using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.Integration.Datasets.DTOs;
using SmkDoc.Domain.Interfaces;
using SmkDoc.Domain.ValueObjects.Ids;

namespace SmkDoc.Application.Modules.Integration.Datasets.Queries.GetDatasetById;

public record GetDatasetByIdQuery(Guid Id);

public sealed class GetDatasetByIdUseCase(
    IDatasetRepository datasetRepo,
    IDataConnectionRepository connectionRepo) : IUseCase<GetDatasetByIdQuery, DatasetDto?>
{
    public async Task<DatasetDto?> ExecuteAsync(GetDatasetByIdQuery query, CancellationToken ct = default)
    {
        var dataset = await datasetRepo.GetByIdAsync(new DatasetId(query.Id), ct);
        if (dataset == null) return null;

        var connection = dataset.DataConnection ?? await connectionRepo.GetByIdAsync(dataset.DataConnectionId, ct);

        return new DatasetDto(
            dataset.Id.Value,
            dataset.Name.Value,
            dataset.Description,
            dataset.DataConnectionId.Value,
            connection?.Name.Value ?? string.Empty,
            dataset.SqlQuery,
            dataset.CacheSeconds,
            dataset.CreatedAt,
            dataset.UpdatedAt);
    }
}
