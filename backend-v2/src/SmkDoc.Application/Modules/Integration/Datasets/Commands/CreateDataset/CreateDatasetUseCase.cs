using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.Integration.Datasets.DTOs;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Exceptions;
using SmkDoc.Domain.Interfaces;

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
    IUnitOfWork unitOfWork) : IUseCase<CreateDatasetCommand, DatasetDto>
{
    private readonly IDatasetRepository _datasetRepo = datasetRepo;
    private readonly IDataConnectionRepository _connectionRepo = connectionRepo;
    private readonly IUnitOfWork _unitOfWork = unitOfWork;

    public async Task<DatasetDto> ExecuteAsync(CreateDatasetCommand command, CancellationToken ct = default)
    {
        var connection = await _connectionRepo.GetByIdAsync(command.DataConnectionId, ct)
            ?? throw new NotFoundException($"DataConnection '{command.DataConnectionId}' not found.");

        var entity = new Dataset(
            command.Name,
            command.Description,
            command.DataConnectionId,
            command.SqlQuery,
            command.CacheSeconds);

        await _datasetRepo.AddAsync(entity, ct);
        await _unitOfWork.CommitAsync(ct);

        return new DatasetDto
        {
            Id = entity.Id,
            Name = entity.Name,
            Description = entity.Description,
            DataConnectionId = entity.DataConnectionId,
            DataConnectionName = connection.Name,
            SqlQuery = entity.SqlQuery,
            CacheSeconds = entity.CacheSeconds,
            CreatedAt = entity.CreatedAt,
            UpdatedAt = entity.UpdatedAt
        };
    }
}
