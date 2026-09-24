using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.DTOs.DataConnections;
using SmkDoc.Domain.Entities;

namespace SmkDoc.Application.UseCases.DataConnections;

public class DataConnectionUseCase
{
    private readonly IRepository<DataConnection> _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDataProtectionService _dataProtection;
    private readonly ISqlExecutorService _sqlExecutor;

    public DataConnectionUseCase(
        IRepository<DataConnection> repository,
        IUnitOfWork unitOfWork,
        IDataProtectionService dataProtection,
        ISqlExecutorService sqlExecutor)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
        _dataProtection = dataProtection;
        _sqlExecutor = sqlExecutor;
    }

    public async Task<List<DataConnectionDto>> GetAllAsync()
    {
        var connections = await _repository.ListAsync(c => true);
        return connections.Select(c => new DataConnectionDto
        {
            Id = c.Id,
            Name = c.Name,
            Provider = c.Provider,
            CreatedAt = c.CreatedAt,
            UpdatedAt = c.UpdatedAt
        }).ToList();
    }

    public async Task<DataConnectionDto?> GetByIdAsync(Guid id)
    {
        var c = await _repository.GetByIdAsync(id);
        if (c == null) return null;

        return new DataConnectionDto
        {
            Id = c.Id,
            Name = c.Name,
            Provider = c.Provider,
            CreatedAt = c.CreatedAt,
            UpdatedAt = c.UpdatedAt
        };
    }

    public async Task<DataConnectionDto> CreateAsync(CreateDataConnectionDto dto)
    {
        var entity = new DataConnection
        {
            Id = Guid.NewGuid(),
            Name = dto.Name,
            Provider = dto.Provider,
            EncryptedConnectionString = _dataProtection.Encrypt(dto.ConnectionString)
        };

        await _repository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync();

        return new DataConnectionDto
        {
            Id = entity.Id,
            Name = entity.Name,
            Provider = entity.Provider,
            CreatedAt = entity.CreatedAt,
            UpdatedAt = entity.UpdatedAt
        };
    }

    public async Task<DataConnectionDto?> UpdateAsync(Guid id, UpdateDataConnectionDto dto)
    {
        var entity = await _repository.GetByIdAsync(id);
        if (entity == null) return null;

        entity.Name = dto.Name;
        entity.Provider = dto.Provider;
        if (!string.IsNullOrEmpty(dto.ConnectionString))
        {
            entity.EncryptedConnectionString = _dataProtection.Encrypt(dto.ConnectionString);
        }
        entity.UpdatedAt = DateTimeOffset.UtcNow;

        _repository.Update(entity);
        await _unitOfWork.SaveChangesAsync();

        return new DataConnectionDto
        {
            Id = entity.Id,
            Name = entity.Name,
            Provider = entity.Provider,
            CreatedAt = entity.CreatedAt,
            UpdatedAt = entity.UpdatedAt
        };
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        var entity = await _repository.GetByIdAsync(id);
        if (entity == null) return false;

        _repository.Remove(entity);
        await _unitOfWork.SaveChangesAsync();
        return true;
    }

    public async Task<bool> TestConnectionAsync(TestDataConnectionDto dto)
    {
        try
        {
            // Simple SELECT 1 to verify connectivity
            var testQuery = "SELECT 1";
            await _sqlExecutor.ExecuteQueryAsJsonAsync(dto.Provider, dto.ConnectionString, testQuery);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
