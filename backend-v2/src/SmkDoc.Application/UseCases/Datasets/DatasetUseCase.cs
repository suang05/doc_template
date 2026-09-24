using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.DTOs.Datasets;
using SmkDoc.Domain.Entities;

namespace SmkDoc.Application.UseCases.Datasets;

public class DatasetUseCase
{
    private readonly IRepository<Dataset> _datasetRepo;
    private readonly IRepository<DataConnection> _connectionRepo;
    private readonly IUnitOfWork _unitOfWork;

    public DatasetUseCase(
        IRepository<Dataset> datasetRepo,
        IRepository<DataConnection> connectionRepo,
        IUnitOfWork unitOfWork)
    {
        _datasetRepo    = datasetRepo;
        _connectionRepo = connectionRepo;
        _unitOfWork     = unitOfWork;
    }

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
            ?? throw new KeyNotFoundException($"DataConnection '{dto.DataConnectionId}' not found.");

        var entity = new Dataset
        {
            Id               = Guid.NewGuid(),
            Name             = dto.Name,
            Description      = dto.Description,
            DataConnectionId = dto.DataConnectionId,
            SqlQuery         = dto.SqlQuery,
            CacheSeconds     = dto.CacheSeconds,
        };

        await _datasetRepo.AddAsync(entity, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return ToDto(entity, new Dictionary<Guid, DataConnection> { [connection.Id] = connection });
    }

    public async Task<DatasetDto?> UpdateAsync(Guid id, UpdateDatasetDto dto, CancellationToken ct = default)
    {
        var entity = await _datasetRepo.GetByIdAsync(id, ct);
        if (entity == null) return null;

        var connection = await _connectionRepo.GetByIdAsync(dto.DataConnectionId, ct)
            ?? throw new KeyNotFoundException($"DataConnection '{dto.DataConnectionId}' not found.");

        entity.Name             = dto.Name;
        entity.Description      = dto.Description;
        entity.DataConnectionId = dto.DataConnectionId;
        entity.SqlQuery         = dto.SqlQuery;
        entity.CacheSeconds     = dto.CacheSeconds;
        entity.UpdatedAt        = DateTimeOffset.UtcNow;

        _datasetRepo.Update(entity);
        await _unitOfWork.SaveChangesAsync(ct);

        return ToDto(entity, new Dictionary<Guid, DataConnection> { [connection.Id] = connection });
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await _datasetRepo.GetByIdAsync(id, ct);
        if (entity == null) return false;

        _datasetRepo.Remove(entity);
        await _unitOfWork.SaveChangesAsync(ct);
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
