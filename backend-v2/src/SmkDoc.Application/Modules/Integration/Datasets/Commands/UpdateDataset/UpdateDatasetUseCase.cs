using SmkDoc.Application.Common.Exceptions;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.Integration.Datasets.DTOs;
using SmkDoc.Domain.Interfaces;
using SmkDoc.Domain.ValueObjects;
using SmkDoc.Domain.ValueObjects.Ids;

namespace SmkDoc.Application.Modules.Integration.Datasets.Commands.UpdateDataset;

public sealed class UpdateDatasetUseCase(
    IDatasetRepository datasetRepo,
    IDataConnectionRepository connectionRepo,
    IUnitOfWork unitOfWork,
    ISqlConnectionValidator sqlValidator,
    TimeProvider timeProvider) : IUseCase<UpdateDatasetCommand, DatasetDto>
{
    public async Task<DatasetDto> ExecuteAsync(UpdateDatasetCommand command, CancellationToken ct = default)
    {
        sqlValidator.ValidateReadOnlySql(command.SqlQuery);

        var entity = await datasetRepo.GetByIdAsync(new DatasetId(command.Id), ct)
            ?? throw new NotFoundException("Dataset", command.Id);

        var connection = await connectionRepo.GetByIdAsync(new DataConnectionId(command.DataConnectionId), ct)
            ?? throw new NotFoundException($"DataConnection '{command.DataConnectionId}' not found.");

        var now = timeProvider.GetUtcNow();
        entity.UpdateDetails(
            DatasetName.Create(command.Name),
            command.Description,
            new DataConnectionId(command.DataConnectionId),
            SqlQuery.Create(command.SqlQuery),
            command.CacheSeconds,
            now);

        datasetRepo.Update(entity);
        await unitOfWork.CommitAsync(ct);

        return new DatasetDto(
            entity.Id.Value,
            entity.Name.Value,
            entity.Description,
            entity.DataConnectionId.Value,
            connection.Name.Value,
            entity.SqlQuery,
            entity.CacheSeconds,
            entity.CreatedAt,
            entity.UpdatedAt);
    }
}
