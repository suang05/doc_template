using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.Integration.Datasets.DTOs;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Interfaces;
using SmkDoc.Domain.Exceptions;
using SmkDoc.Domain.ValueObjects;
using SmkDoc.Domain.ValueObjects.Ids;

namespace SmkDoc.Application.Modules.Integration.Datasets;

[Obsolete("Use single-intent UseCases instead (CreateDatasetUseCase, UpdateDatasetUseCase, etc.).")]
public sealed class DatasetUseCase(
    IDatasetRepository datasetRepo,
    IDataConnectionRepository connectionRepo,
    IUnitOfWork unitOfWork,
    TimeProvider? timeProvider = null)
{
    private readonly TimeProvider timeProvider = timeProvider ?? TimeProvider.System;

    public async Task<List<DatasetDto>> GetAllAsync(CancellationToken ct = default)
    {
        var datasets     = await datasetRepo.ListAsync(ct);
        var connectionIds = datasets.Select(d => (Guid)d.DataConnectionId).Distinct().ToList();
        var connections  = await connectionRepo.GetByIdsAsync(connectionIds, ct);
        var connMap      = connections.ToDictionary(c => c.Id);

        return datasets.Select(d => ToDto(d, connMap)).ToList();
    }

    public async Task<DatasetDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var dataset = await datasetRepo.GetByIdAsync(id, ct);
        if (dataset == null) return null;

        var connection = await connectionRepo.GetByIdAsync(dataset.DataConnectionId, ct);
        return ToDto(dataset, connection == null
            ? new Dictionary<DataConnectionId, DataConnection>()
            : new Dictionary<DataConnectionId, DataConnection> { [connection.Id] = connection });
    }

    public async Task<DatasetDto> CreateAsync(CreateDatasetDto dto, CancellationToken ct = default)
    {
        var connection = await connectionRepo.GetByIdAsync(dto.DataConnectionId, ct)
            ?? throw new NotFoundException($"DataConnection '{dto.DataConnectionId}' not found.");

        var now = this.timeProvider.GetUtcNow();
        var entity = Dataset.Create(DatasetName.Create(dto.Name), dto.Description, dto.DataConnectionId, dto.SqlQuery, dto.CacheSeconds, now);

        await datasetRepo.AddAsync(entity, ct);
        await unitOfWork.CommitAsync(ct);

        return ToDto(entity, new Dictionary<DataConnectionId, DataConnection> { [connection.Id] = connection });
    }

    public async Task<DatasetDto?> UpdateAsync(Guid id, UpdateDatasetDto dto, CancellationToken ct = default)
    {
        var entity = await datasetRepo.GetByIdAsync(id, ct);
        if (entity == null) return null;

        var connection = await connectionRepo.GetByIdAsync(dto.DataConnectionId, ct)
            ?? throw new NotFoundException($"DataConnection '{dto.DataConnectionId}' not found.");

        var now = this.timeProvider.GetUtcNow();
        entity.UpdateDetails(DatasetName.Create(dto.Name), dto.Description, dto.DataConnectionId, dto.SqlQuery, dto.CacheSeconds, now);

        datasetRepo.Update(entity);
        await unitOfWork.CommitAsync(ct);

        return ToDto(entity, new Dictionary<DataConnectionId, DataConnection> { [connection.Id] = connection });
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await datasetRepo.GetByIdAsync(id, ct);
        if (entity == null) return false;

        datasetRepo.Remove(entity);
        await unitOfWork.CommitAsync(ct);
        return true;
    }

    private static DatasetDto ToDto(Dataset d, Dictionary<DataConnectionId, DataConnection> connMap) => new()
    {
        Id                 = d.Id,
        Name               = d.Name.Value,
        Description        = d.Description,
        DataConnectionId   = d.DataConnectionId,
        DataConnectionName = connMap.TryGetValue(d.DataConnectionId, out var c) ? c.Name.Value : string.Empty,
        SqlQuery           = d.SqlQuery,
        CacheSeconds       = d.CacheSeconds,
        CreatedAt          = d.CreatedAt,
        UpdatedAt          = d.UpdatedAt,
    };
}
