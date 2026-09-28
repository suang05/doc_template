using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.Integration.DataConnections.DTOs;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Interfaces;

namespace SmkDoc.Application.Modules.Integration.DataConnections;

public sealed class DataConnectionUseCase(
    IRepository<DataConnection> repository,
    IUnitOfWork unitOfWork,
    IDataProtectionService dataProtection,
    ISqlExecutorService sqlExecutor)
{
    private readonly IRepository<DataConnection> _repository = repository;
    private readonly IUnitOfWork _unitOfWork = unitOfWork;
    private readonly IDataProtectionService _dataProtection = dataProtection;
    private readonly ISqlExecutorService _sqlExecutor = sqlExecutor;

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
        var entity = new DataConnection(dto.Name, dto.Provider, _dataProtection.Encrypt(dto.ConnectionString));

        await _repository.AddAsync(entity);
        await _unitOfWork.CommitAsync();

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

        entity.UpdateConnection(dto.Name, dto.Provider, string.IsNullOrEmpty(dto.ConnectionString) ? entity.EncryptedConnectionString : _dataProtection.Encrypt(dto.ConnectionString));

        _repository.Update(entity);
        await _unitOfWork.CommitAsync();

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
        await _unitOfWork.CommitAsync();
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
