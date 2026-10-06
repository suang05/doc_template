using SmkDoc.Domain.Common;
using SmkDoc.Domain.Exceptions;
using SmkDoc.Domain.ValueObjects;

namespace SmkDoc.Domain.Entities;

/// <summary>
/// Domain entity representing a dataset query definition associated with a data connection.
/// </summary>
public sealed class Dataset : BaseEntity
{
    public DatasetName Name { get; private set; } = null!;
    public string? Description { get; private set; }
    public Guid DataConnectionId { get; private set; }
    public string SqlQuery { get; private set; } = string.Empty;
    public int CacheSeconds { get; private set; }

    // Navigation property for EF Core materialization
    public DataConnection? DataConnection { get; private set; }

    // For EF Core materialization only
    private Dataset() { }

    internal Dataset(
        Guid? id,
        DatasetName name,
        string? description,
        Guid dataConnectionId,
        string sqlQuery,
        int cacheSeconds,
        DateTimeOffset now)
        : base(id, createdAt: now)
    {
        Name = Guard.NotNull(name, nameof(Name));
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        DataConnectionId = Guard.NotEmpty(dataConnectionId, nameof(DataConnectionId));
        SqlQuery = Guard.NotBlank(sqlQuery, nameof(SqlQuery));
        if (cacheSeconds < 0)
        {
            throw new DomainValidationException("CacheSeconds cannot be negative.");
        }
        CacheSeconds = cacheSeconds;
    }

    public static Dataset Create(
        DatasetName name,
        string? description,
        Guid dataConnectionId,
        string sqlQuery,
        int cacheSeconds,
        DateTimeOffset now) =>
        new(null, name, description, dataConnectionId, sqlQuery, cacheSeconds, now);

    public void UpdateDetails(
        DatasetName name,
        string? description,
        Guid dataConnectionId,
        string sqlQuery,
        int cacheSeconds,
        DateTimeOffset now)
    {
        Name = Guard.NotNull(name, nameof(Name));
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        DataConnectionId = Guard.NotEmpty(dataConnectionId, nameof(DataConnectionId));
        SqlQuery = Guard.NotBlank(sqlQuery, nameof(SqlQuery));
        if (cacheSeconds < 0)
        {
            throw new DomainValidationException("CacheSeconds cannot be negative.");
        }
        CacheSeconds = cacheSeconds;
        SetUpdated(now);
    }
}
