using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.DTOs.Datasets;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Interfaces;
using SmkDoc.Domain.Exceptions;

namespace SmkDoc.Application.UseCases.Datasets;

public sealed class DatasetUseCase(
    IRepository<Dataset> datasetRepo,
    IRepository<DataConnection> connectionRepo,
    IUnitOfWork unitOfWork)
{
    private readonly IRepository<Dataset> _datasetRepo = datasetRepo;
    private readonly IRepository<DataConnection> _connectionRepo = connectionRepo;
    private readonly IUnitOfWork _unitOfWork = unitOfWork;

    public async Task<List<DatasetDto>> GetAllAsync(CancellationToken ct = default)
    {
        var datasets     = await _datasetRepo.ListAsync(_ => true, ct);
        var connectionIds = datasets.Select(d => d.DataConnectionId).Distinct().ToList();
        var connections  = await _connectionRepo.ListAsync(c => connectionIds.Contains(c.Id), ct);
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

        var entity = new Dataset(dto.Name, dto.Description, dto.DataConnectionId, dto.SqlQuery, dto.CacheSeconds);

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

        entity.UpdateDetails(dto.Name, dto.Description, dto.DataConnectionId, dto.SqlQuery, dto.CacheSeconds);

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
        Name               = d.Name,
        Description        = d.Description,
        DataConnectionId   = d.DataConnectionId,
        DataConnectionName = connMap.TryGetValue(d.DataConnectionId, out var c) ? c.Name : string.Empty,
        SqlQuery           = d.SqlQuery,
        CacheSeconds       = d.CacheSeconds,
        CreatedAt          = d.CreatedAt,
        UpdatedAt          = d.UpdatedAt,
    };
}
