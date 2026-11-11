using SmkDoc.Application.Common.Exceptions;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.Integration.Datasets.DTOs;
using SmkDoc.Application.Modules.Integration.Datasets.Services;
using SmkDoc.Domain.Interfaces;
using SmkDoc.Domain.ValueObjects;
using SmkDoc.Domain.ValueObjects.Ids;

namespace SmkDoc.Application.Modules.Integration.Datasets.Commands.UpdateDataset;

public sealed class UpdateDatasetUseCase(
    IDatasetRepository datasetRepo,
    IUnitOfWork unitOfWork,
    IDatasetResolver datasetResolver,
    TimeProvider timeProvider) : IUseCase<UpdateDatasetCommand, DatasetResultDto>
{
    public async Task<DatasetResultDto> ExecuteAsync(UpdateDatasetCommand command, CancellationToken ct = default)
    {
        var connection = await datasetResolver.ValidateAndResolveConnectionAsync(
            new DataConnectionId(command.DataConnectionId),
            command.SqlQuery,
            ct);

        var entity = await datasetRepo.GetByIdAsync(new DatasetId(command.Id), ct)
            ?? throw new NotFoundException("Dataset", command.Id);

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

        return new DatasetResultDto(
            entity.Id.Value,
            entity.Name.Value,
            entity.Description,
            entity.DataConnectionId.Value,
            connection.Name.Value,
            entity.SqlQuery,
            entity.CacheSeconds,
            entity.CreatedAt,
            entity.UpdatedAt,
            entity.GetConcurrencyToken());
    }
}
