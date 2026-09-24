using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SmkDocServer.Domain.Interfaces;

public class DataConnectionDto
{
    public Guid? Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public Guid? ProjectId { get; set; }
    public string DatabaseType { get; set; } = "postgresql";
    /// <summary>Plain-text connection string (never returned to client after save)</summary>
    public string? ConnectionString { get; set; }
}

public interface IDataConnectionService
{
    Task<List<DataConnectionDto>> GetAllAsync(Guid? projectId = null);
    Task<DataConnectionDto> CreateAsync(DataConnectionDto dto);
    Task<DataConnectionDto> UpdateAsync(Guid id, DataConnectionDto dto);
    Task DeleteAsync(Guid id);
    Task<bool> TestAsync(Guid id);
    /// <summary>Internal use only — returns decrypted connection string</summary>
    Task<string?> GetDecryptedConnectionStringAsync(Guid id);
}
