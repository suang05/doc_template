using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.Integration.Datasets.DTOs;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Exceptions;
using SmkDoc.Domain.Interfaces;
using SmkDoc.Domain.ValueObjects;

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
    TimeProvider? timeProvider = null) : IUseCase<CreateDatasetCommand, DatasetDto>
{
    private readonly IDatasetRepository _datasetRepo = datasetRepo;
    private readonly IDataConnectionRepository _connectionRepo = connectionRepo;
    private readonly IUnitOfWork _unitOfWork = unitOfWork;
    private readonly TimeProvider _timeProvider = timeProvider ?? TimeProvider.System;

    public async Task<DatasetDto> ExecuteAsync(CreateDatasetCommand command, CancellationToken ct = default)
    {
        var connection = await _connectionRepo.GetByIdAsync(command.DataConnectionId, ct)
            ?? throw new NotFoundException($"DataConnection '{command.DataConnectionId}' not found.");

        var now = _timeProvider.GetUtcNow();
        var entity = Dataset.Create(
            DatasetName.Create(command.Name),
            command.Description,
            command.DataConnectionId,
            command.SqlQuery,
            command.CacheSeconds,
            now);

        await _datasetRepo.AddAsync(entity, ct);
        await _unitOfWork.CommitAsync(ct);

        return new DatasetDto
        {
            Id = entity.Id,
            Name = entity.Name.Value,
            Description = entity.Description,
            DataConnectionId = entity.DataConnectionId,
            DataConnectionName = connection.Name.Value,
            SqlQuery = entity.SqlQuery,
            CacheSeconds = entity.CacheSeconds,
            CreatedAt = entity.CreatedAt,
            UpdatedAt = entity.UpdatedAt
        };
    }
}
