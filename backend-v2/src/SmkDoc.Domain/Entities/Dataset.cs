using SmkDoc.Domain.Common;
using SmkDoc.Domain.Exceptions;
using SmkDoc.Domain.Interfaces;
using SmkDoc.Domain.ValueObjects;
using SmkDoc.Domain.ValueObjects.Ids;

namespace SmkDoc.Domain.Entities;

/// <summary>
/// Domain entity representing a dataset query definition associated with a data connection.
/// </summary>
public sealed class Dataset : BaseEntity<DatasetId>, IMustHaveProject
{
    public ProjectId ProjectId { get; private set; }
    public DatasetName Name { get; private set; } = null!;
    public string? Description { get; private set; }
    public DataConnectionId DataConnectionId { get; private set; }
    public SqlQuery SqlQuery { get; private set; } = null!;
    public int CacheSeconds { get; private set; }

    // Navigation property for EF Core materialization
    public DataConnection? DataConnection { get; private set; }

    // For EF Core materialization only
    private Dataset() { }

    internal Dataset(
        DatasetId? id,
        DatasetName name,
        string? description,
        DataConnectionId dataConnectionId,
        SqlQuery sqlQuery,
        int cacheSeconds,
        DateTimeOffset now,
        ProjectId projectId)
        : base(id.HasValue && !id.Value.IsEmpty ? id.Value : DatasetId.New(), createdAt: now)
    {
        Name = Guard.NotNull(name, nameof(Name));
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        DataConnectionId = Guard.NotEmpty(dataConnectionId, nameof(DataConnectionId));
        SqlQuery = Guard.NotNull(sqlQuery, nameof(SqlQuery));
        if (cacheSeconds < 0)
        {
            throw new DomainValidationException("CacheSeconds cannot be negative.");
        }
        CacheSeconds = cacheSeconds;
        ProjectId = Guard.NotEmpty(projectId, nameof(ProjectId));
    }

    public static Dataset Create(
        DatasetName name,
        string? description,
        DataConnectionId dataConnectionId,
        SqlQuery sqlQuery,
        int cacheSeconds,
        DateTimeOffset now,
        ProjectId projectId) =>
        new(null, name, description, dataConnectionId, sqlQuery, cacheSeconds, now, projectId);

    public void UpdateDetails(
        DatasetName name,
        string? description,
        DataConnectionId dataConnectionId,
        SqlQuery sqlQuery,
        int cacheSeconds,
        DateTimeOffset now)
    {
        Name = Guard.NotNull(name, nameof(Name));
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        DataConnectionId = Guard.NotEmpty(dataConnectionId, nameof(DataConnectionId));
        SqlQuery = Guard.NotNull(sqlQuery, nameof(SqlQuery));
        if (cacheSeconds < 0)
        {
            throw new DomainValidationException("CacheSeconds cannot be negative.");
        }
        CacheSeconds = cacheSeconds;
        SetUpdated(now);
    }
}
