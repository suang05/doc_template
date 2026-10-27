using FluentValidation;
using SmkDoc.Application.Common.Exceptions;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.Integration.Datasets.DTOs;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Exceptions;
using SmkDoc.Domain.Interfaces;
using SmkDoc.Domain.ValueObjects;
using SmkDoc.Domain.ValueObjects.Ids;

namespace SmkDoc.Application.Modules.Integration.Datasets.Commands.CreateDataset;

public record CreateDatasetCommand(
    string Name,
    string? Description,
    Guid DataConnectionId,
    string SqlQuery,
    int CacheSeconds = 0);

public sealed class CreateDatasetUseCase(
    IDatasetRepository datasetRepo,
    IDataConnectionRepository connectionRepo,
    IUnitOfWork unitOfWork,
    IValidator<CreateDatasetCommand> validator,
    TimeProvider? timeProvider = null,
    ICurrentProjectContext? projectContext = null) : IUseCase<CreateDatasetCommand, DatasetDto>
{
    private readonly TimeProvider timeProvider = timeProvider ?? TimeProvider.System;

    public async Task<DatasetDto> ExecuteAsync(CreateDatasetCommand command, CancellationToken ct = default)
    {
        var validationResult = await validator.ValidateAsync(command, ct);
        if (!validationResult.IsValid)
        {
            throw new ValidationException(validationResult.ToDictionary());
        }

        var connection = await connectionRepo.GetByIdAsync(new DataConnectionId(command.DataConnectionId), ct)
            ?? throw new NotFoundException($"DataConnection '{command.DataConnectionId}' not found.");

        var now = this.timeProvider.GetUtcNow();
        var entity = Dataset.Create(
            DatasetName.Create(command.Name),
            command.Description,
            new DataConnectionId(command.DataConnectionId),
            SqlQuery.Create(command.SqlQuery),
            command.CacheSeconds,
            now,
            projectContext?.ProjectId ?? default);

        await datasetRepo.AddAsync(entity, ct);
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
