using SmkDoc.Domain.Common;
using SmkDoc.Domain.Exceptions;

namespace SmkDoc.Domain.Entities;

/// <summary>
/// Domain entity representing a dataset query definition associated with a data connection.
/// </summary>
public class Dataset : BaseEntity
{
    public string Name { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public Guid DataConnectionId { get; private set; }
    public string SqlQuery { get; private set; } = string.Empty;
    public int CacheSeconds { get; private set; } = 0;

    // Navigation
    public virtual DataConnection? DataConnection { get; private set; }

    // For EF Core materialization
    private Dataset() { }

    public Dataset(string name, string? description, Guid dataConnectionId, string sqlQuery, int cacheSeconds = 0, Guid? id = null)
        : base(id)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainValidationException("Dataset name cannot be empty or whitespace.");
        }

        if (dataConnectionId == Guid.Empty)
        {
            throw new DomainValidationException("DataConnectionId cannot be empty.");
        }

        if (string.IsNullOrWhiteSpace(sqlQuery))
        {
            throw new DomainValidationException("SqlQuery cannot be empty or whitespace.");
        }

        if (cacheSeconds < 0)
        {
            throw new DomainValidationException("CacheSeconds cannot be negative.");
        }

        Name = name.Trim();
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        DataConnectionId = dataConnectionId;
        SqlQuery = sqlQuery;
        CacheSeconds = cacheSeconds;
    }

    public void UpdateDetails(string name, string? description, Guid dataConnectionId, string sqlQuery, int cacheSeconds)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainValidationException("Dataset name cannot be empty or whitespace.");
        }

        if (dataConnectionId == Guid.Empty)
        {
            throw new DomainValidationException("DataConnectionId cannot be empty.");
        }

        if (string.IsNullOrWhiteSpace(sqlQuery))
        {
            throw new DomainValidationException("SqlQuery cannot be empty or whitespace.");
        }

        if (cacheSeconds < 0)
        {
            throw new DomainValidationException("CacheSeconds cannot be negative.");
        }

        Name = name.Trim();
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        DataConnectionId = dataConnectionId;
        SqlQuery = sqlQuery;
        CacheSeconds = cacheSeconds;
        SetUpdated();
    }
}
