using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.Integration.Datasets.DTOs;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Interfaces;
using SmkDoc.Domain.Exceptions;
using SmkDoc.Domain.ValueObjects;

namespace SmkDoc.Application.Modules.Integration.Datasets;

[Obsolete("Use single-intent UseCases instead (CreateDatasetUseCase, UpdateDatasetUseCase, etc.).")]
public sealed class DatasetUseCase(
    IDatasetRepository datasetRepo,
    IDataConnectionRepository connectionRepo,
    IUnitOfWork unitOfWork,
    TimeProvider? timeProvider = null)
{
    private readonly IDatasetRepository _datasetRepo = datasetRepo;
    private readonly IDataConnectionRepository _connectionRepo = connectionRepo;
    private readonly IUnitOfWork _unitOfWork = unitOfWork;
    private readonly TimeProvider _timeProvider = timeProvider ?? TimeProvider.System;

    public async Task<List<DatasetDto>> GetAllAsync(CancellationToken ct = default)
    {
        var datasets     = await _datasetRepo.ListAsync(ct);
        var connectionIds = datasets.Select(d => d.DataConnectionId).Distinct().ToList();
        var connections  = await _connectionRepo.GetByIdsAsync(connectionIds, ct);
        var connMap      = connections.ToDictionary(c => c.Id);

        return datasets.Select(d => ToDto(d, connMap)).ToList();
    }

    public async Task<DatasetDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var dataset = await _datasetRepo.GetByIdAsync(id, ct);
        if (dataset == null) return null;

        var connection = await _connectionRepo.GetByIdAsync(dataset.DataConnectionId, ct);
        return ToDto(dataset, connection == null
            ? new Dictionary<Guid, DataConnection>()
            : new Dictionary<Guid, DataConnection> { [connection.Id] = connection });
    }

    public async Task<DatasetDto> CreateAsync(CreateDatasetDto dto, CancellationToken ct = default)
    {
        var connection = await _connectionRepo.GetByIdAsync(dto.DataConnectionId, ct)
            ?? throw new NotFoundException($"DataConnection '{dto.DataConnectionId}' not found.");

        var now = _timeProvider.GetUtcNow();
        var entity = Dataset.Create(DatasetName.Create(dto.Name), dto.Description, dto.DataConnectionId, dto.SqlQuery, dto.CacheSeconds, now);

        await _datasetRepo.AddAsync(entity, ct);
        await _unitOfWork.CommitAsync(ct);

        return ToDto(entity, new Dictionary<Guid, DataConnection> { [connection.Id] = connection });
    }

    public async Task<DatasetDto?> UpdateAsync(Guid id, UpdateDatasetDto dto, CancellationToken ct = default)
    {
        var entity = await _datasetRepo.GetByIdAsync(id, ct);
        if (entity == null) return null;

        var connection = await _connectionRepo.GetByIdAsync(dto.DataConnectionId, ct)
            ?? throw new NotFoundException($"DataConnection '{dto.DataConnectionId}' not found.");

        var now = _timeProvider.GetUtcNow();
        entity.UpdateDetails(DatasetName.Create(dto.Name), dto.Description, dto.DataConnectionId, dto.SqlQuery, dto.CacheSeconds, now);

        _datasetRepo.Update(entity);
        await _unitOfWork.CommitAsync(ct);

        return ToDto(entity, new Dictionary<Guid, DataConnection> { [connection.Id] = connection });
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await _datasetRepo.GetByIdAsync(id, ct);
        if (entity == null) return false;

        _datasetRepo.Remove(entity);
        await _unitOfWork.CommitAsync(ct);
        return true;
    }

    private static DatasetDto ToDto(Dataset d, Dictionary<Guid, DataConnection> connMap) => new()
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
