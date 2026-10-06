using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.Integration.DataConnections.DTOs;
using SmkDoc.Domain.Common;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Enums;
using SmkDoc.Domain.Interfaces;
using SmkDoc.Domain.ValueObjects;

namespace SmkDoc.Application.Modules.Integration.DataConnections;

[Obsolete("Use single-intent UseCases instead (CreateDataConnectionUseCase, UpdateDataConnectionUseCase, etc.).")]
public sealed class DataConnectionUseCase(
    IDataConnectionRepository repository,
    IUnitOfWork unitOfWork,
    IDataProtectionService dataProtection,
    ISqlExecutorService sqlExecutor,
    TimeProvider? timeProvider = null)
{
    private readonly IDataConnectionRepository _repository = repository;
    private readonly IUnitOfWork _unitOfWork = unitOfWork;
    private readonly IDataProtectionService _dataProtection = dataProtection;
    private readonly ISqlExecutorService _sqlExecutor = sqlExecutor;
    private readonly TimeProvider _timeProvider = timeProvider ?? TimeProvider.System;

    public async Task<List<DataConnectionDto>> GetAllAsync()
    {
        var connections = await _repository.ListAsync();
        return connections.Select(c => new DataConnectionDto
        {
            Id = c.Id,
            Name = c.Name.Value,
            Provider = c.Provider.Name,
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
            Name = c.Name.Value,
            Provider = c.Provider.Name,
            CreatedAt = c.CreatedAt,
            UpdatedAt = c.UpdatedAt
        };
    }

    public async Task<DataConnectionDto> CreateAsync(CreateDataConnectionDto dto)
    {
        var now = _timeProvider.GetUtcNow();
        var entity = DataConnection.Create(
            ConnectionName.Create(dto.Name),
            Enumeration.FromDisplayName<DatabaseProvider>(dto.Provider),
            _dataProtection.Encrypt(dto.ConnectionString),
            now);

        await _repository.AddAsync(entity);
        await _unitOfWork.CommitAsync();

        return new DataConnectionDto
        {
            Id = entity.Id,
            Name = entity.Name.Value,
            Provider = entity.Provider.Name,
            CreatedAt = entity.CreatedAt,
            UpdatedAt = entity.UpdatedAt
        };
    }

    public async Task<DataConnectionDto?> UpdateAsync(Guid id, UpdateDataConnectionDto dto)
    {
        var entity = await _repository.GetByIdAsync(id);
        if (entity == null) return null;

        var now = _timeProvider.GetUtcNow();
        entity.UpdateConnection(
            ConnectionName.Create(dto.Name),
            Enumeration.FromDisplayName<DatabaseProvider>(dto.Provider),
            string.IsNullOrEmpty(dto.ConnectionString) ? entity.EncryptedConnectionString : _dataProtection.Encrypt(dto.ConnectionString),
            now);

        _repository.Update(entity);
        await _unitOfWork.CommitAsync();

        return new DataConnectionDto
        {
            Id = entity.Id,
            Name = entity.Name.Value,
            Provider = entity.Provider.Name,
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
