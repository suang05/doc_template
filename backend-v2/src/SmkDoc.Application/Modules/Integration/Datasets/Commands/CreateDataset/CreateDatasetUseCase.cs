using SmkDoc.Application.Common.Extensions;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.Integration.Datasets.DTOs;
using SmkDoc.Application.Modules.Integration.Datasets.Services;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Interfaces;
using SmkDoc.Domain.ValueObjects;
using SmkDoc.Domain.ValueObjects.Ids;

namespace SmkDoc.Application.Modules.Integration.Datasets.Commands.CreateDataset;

public sealed class CreateDatasetUseCase(
    IDatasetRepository datasetRepo,
    IUnitOfWork unitOfWork,
    IDatasetResolver datasetResolver,
    ICurrentProjectContext projectContext,
    TimeProvider timeProvider) : IUseCase<CreateDatasetCommand, DatasetResultDto>
{
    public async Task<DatasetResultDto> ExecuteAsync(CreateDatasetCommand command, CancellationToken ct = default)
    {
        var projectId = projectContext.EnsureResolved();

        var connection = await datasetResolver.ValidateAndResolveConnectionAsync(
            new DataConnectionId(command.DataConnectionId),
            command.SqlQuery,
            ct);

        var now = timeProvider.GetUtcNow();
        var entity = Dataset.Create(
            DatasetName.Create(command.Name),
            command.Description,
            new DataConnectionId(command.DataConnectionId),
            SqlQuery.Create(command.SqlQuery),
            command.CacheSeconds,
            now,
            projectId);

        await datasetRepo.AddAsync(entity, ct);
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
