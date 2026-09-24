using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;
using SmkDocServer.Domain.Entities;
using SmkDocServer.Domain.Interfaces;
using SmkDocServer.Infrastructure.Data;

namespace SmkDocServer.Infrastructure.Services.DataSource;

public class DataConnectionService : IDataConnectionService
{
    private const string ProtectionPurpose = "SmkDocServer.DataConnection.ConnectionString";

    private readonly AppDbContext _db;
    private readonly IDataProtector _protector;
    private readonly ILogger<DataConnectionService> _logger;

    public DataConnectionService(
        AppDbContext db,
        IDataProtectionProvider dataProtection,
        ILogger<DataConnectionService> logger)
    {
        _db = db;
        _protector = dataProtection.CreateProtector(ProtectionPurpose);
        _logger = logger;
    }

    public async Task<List<DataConnectionDto>> GetAllAsync(Guid? projectId = null)
    {
        var query = _db.DataConnections.AsNoTracking();
        if (projectId.HasValue)
            query = query.Where(c => c.ProjectId == projectId);

        return await query
            .OrderBy(c => c.Name)
            .Select(c => new DataConnectionDto
            {
                Id = c.Id,
                Name = c.Name,
                ProjectId = c.ProjectId,
                DatabaseType = c.DatabaseType,
                ConnectionString = null   // never expose encrypted value
            })
            .ToListAsync();
    }

    public async Task<DataConnectionDto> CreateAsync(DataConnectionDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.ConnectionString))
            throw new ArgumentException("ConnectionString is required.");

        var entity = new DataConnection
        {
            Name = dto.Name.Trim(),
            ProjectId = dto.ProjectId,
            DatabaseType = dto.DatabaseType.Trim().ToLower(),
            ConnectionStringEncrypted = _protector.Protect(dto.ConnectionString),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await _db.DataConnections.AddAsync(entity);
        await _db.SaveChangesAsync();

        return new DataConnectionDto { Id = entity.Id, Name = entity.Name, ProjectId = entity.ProjectId, DatabaseType = entity.DatabaseType };
    }

    public async Task<DataConnectionDto> UpdateAsync(Guid id, DataConnectionDto dto)
    {
        var entity = await _db.DataConnections.FindAsync(id)
            ?? throw new KeyNotFoundException($"DataConnection {id} not found.");

        entity.Name = dto.Name.Trim();
        entity.ProjectId = dto.ProjectId;
        entity.DatabaseType = dto.DatabaseType.Trim().ToLower();
        entity.UpdatedAt = DateTime.UtcNow;

        if (!string.IsNullOrWhiteSpace(dto.ConnectionString))
            entity.ConnectionStringEncrypted = _protector.Protect(dto.ConnectionString);

        await _db.SaveChangesAsync();

        return new DataConnectionDto { Id = entity.Id, Name = entity.Name, ProjectId = entity.ProjectId, DatabaseType = entity.DatabaseType };
    }

    public async Task DeleteAsync(Guid id)
    {
        var entity = await _db.DataConnections.FindAsync(id)
            ?? throw new KeyNotFoundException($"DataConnection {id} not found.");
        _db.DataConnections.Remove(entity);
        await _db.SaveChangesAsync();
    }

    public async Task<bool> TestAsync(Guid id)
    {
        string? cs = await GetDecryptedConnectionStringAsync(id);
        if (cs is null) return false;

        try
        {
            await using var conn = new NpgsqlConnection(cs);
            await conn.OpenAsync();
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "DataConnection {Id} test failed.", id);
            return false;
        }
    }

    public async Task<string?> GetDecryptedConnectionStringAsync(Guid id)
    {
        var entity = await _db.DataConnections.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == id);

        if (entity is null) return null;

        try
        {
            return _protector.Unprotect(entity.ConnectionStringEncrypted);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to decrypt connection string for DataConnection {Id}.", id);
            return null;
        }
    }
}
