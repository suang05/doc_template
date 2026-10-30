using FluentValidation;
using SmkDoc.Application.Common.Exceptions;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.Integration.Datasets.DTOs;
using SmkDoc.Domain.Exceptions;
using SmkDoc.Domain.Interfaces;
using SmkDoc.Domain.ValueObjects;
using SmkDoc.Domain.ValueObjects.Ids;

namespace SmkDoc.Application.Modules.Integration.Datasets.Commands.UpdateDataset;

public record UpdateDatasetCommand(
    Guid Id,
    string Name,
    string? Description,
    Guid DataConnectionId,
    string SqlQuery,
    int CacheSeconds = 0);

public sealed class UpdateDatasetUseCase(
    IDatasetRepository datasetRepo,
    IDataConnectionRepository connectionRepo,
    IUnitOfWork unitOfWork,
    IValidator<UpdateDatasetCommand> validator,
    ISqlConnectionValidator sqlValidator,
    TimeProvider? timeProvider = null) : IUseCase<UpdateDatasetCommand, DatasetDto?>
{
    private readonly TimeProvider _timeProvider = timeProvider ?? TimeProvider.System;

    public async Task<DatasetDto?> ExecuteAsync(UpdateDatasetCommand command, CancellationToken ct = default)
    {
        var validationResult = await validator.ValidateAsync(command, ct);
        if (!validationResult.IsValid)
        {
            throw new SmkDoc.Application.Common.Exceptions.ValidationException(validationResult.ToDictionary());
        }

        sqlValidator.ValidateReadOnlySql(command.SqlQuery);

        var entity = await datasetRepo.GetByIdAsync(new DatasetId(command.Id), ct);
        if (entity == null) return null;

        var connection = await connectionRepo.GetByIdAsync(new DataConnectionId(command.DataConnectionId), ct)
            ?? throw new NotFoundException($"DataConnection '{command.DataConnectionId}' not found.");

        var now = _timeProvider.GetUtcNow();
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
