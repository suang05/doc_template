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
    TimeProvider? timeProvider = null) : IUseCase<UpdateDatasetCommand, DatasetDto?>
{
    private readonly IDatasetRepository _datasetRepo = datasetRepo;
    private readonly IDataConnectionRepository _connectionRepo = connectionRepo;
    private readonly IUnitOfWork _unitOfWork = unitOfWork;
    private readonly TimeProvider _timeProvider = timeProvider ?? TimeProvider.System;

    public async Task<DatasetDto?> ExecuteAsync(UpdateDatasetCommand command, CancellationToken ct = default)
    {
        var entity = await _datasetRepo.GetByIdAsync(new DatasetId(command.Id), ct);
        if (entity == null) return null;

        var connection = await _connectionRepo.GetByIdAsync(new DataConnectionId(command.DataConnectionId), ct)
            ?? throw new NotFoundException($"DataConnection '{command.DataConnectionId}' not found.");

        var now = _timeProvider.GetUtcNow();
        entity.UpdateDetails(
            DatasetName.Create(command.Name),
            command.Description,
            new DataConnectionId(command.DataConnectionId),
            SqlQuery.Create(command.SqlQuery),
            command.CacheSeconds,
            now);

        _datasetRepo.Update(entity);
        await _unitOfWork.CommitAsync(ct);

        return new DatasetDto
        {
            Id = entity.Id.Value,
            Name = entity.Name.Value,
            Description = entity.Description,
            DataConnectionId = entity.DataConnectionId.Value,
            DataConnectionName = connection.Name.Value,
            SqlQuery = entity.SqlQuery,
            CacheSeconds = entity.CacheSeconds,
            CreatedAt = entity.CreatedAt,
            UpdatedAt = entity.UpdatedAt
        };
    }
}
