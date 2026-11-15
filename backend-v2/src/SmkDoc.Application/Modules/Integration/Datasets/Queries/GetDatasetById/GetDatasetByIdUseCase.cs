using SmkDoc.Application.Modules.Integration.Datasets.DTOs;
using SmkDoc.Domain.ValueObjects.Ids;

namespace SmkDoc.Application.Modules.Integration.Datasets.Queries.GetDatasetById;

public sealed class GetDatasetByIdUseCase(
    IDatasetRepository datasetRepo,
    IDataConnectionRepository connectionRepo) : IUseCase<GetDatasetByIdQuery, DatasetResultDto>
{
    public async Task<DatasetResultDto> ExecuteAsync(GetDatasetByIdQuery query, CancellationToken ct)
    {
        var dataset = await datasetRepo.GetByIdAsync(new DatasetId(query.Id), ct)
            ?? throw new NotFoundException("Dataset", query.Id);

        var connection = dataset.DataConnection ?? await connectionRepo.GetByIdAsync(dataset.DataConnectionId, ct);

        return dataset.ToResultDto(connection?.Name.Value);
    }
}
