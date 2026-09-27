namespace SmkDoc.Application.DTOs.Datasets;

/// <summary>
/// DTO representing a dataset query and cache configuration.
/// </summary>
public record DatasetDto(
    Guid Id,
    string Name,
    string? Description,
    Guid DataConnectionId,
    string DataConnectionName,
    string SqlQuery,
    int CacheSeconds,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt
)
{
    public DatasetDto() : this(Guid.Empty, string.Empty, null, Guid.Empty, string.Empty, string.Empty, 0, default, null) {}
    public Guid Id { get; init; } = Id;
    public string Name { get; init; } = Name;
    public string? Description { get; init; } = Description;
    public Guid DataConnectionId { get; init; } = DataConnectionId;
    public string DataConnectionName { get; init; } = DataConnectionName;
    public string SqlQuery { get; init; } = SqlQuery;
    public int CacheSeconds { get; init; } = CacheSeconds;
    public DateTimeOffset CreatedAt { get; init; } = CreatedAt;
    public DateTimeOffset? UpdatedAt { get; init; } = UpdatedAt;
}

/// <summary>
/// Command / DTO for creating a new dataset.
/// </summary>
public record CreateDatasetDto
{
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public Guid DataConnectionId { get; init; }
    public string SqlQuery { get; init; } = string.Empty;
    public int CacheSeconds { get; init; } = 0;

    public CreateDatasetDto() {}
    public CreateDatasetDto(string name, string? description, Guid dataConnectionId, string sqlQuery, int cacheSeconds = 0)
    {
        Name = name;
        Description = description;
        DataConnectionId = dataConnectionId;
        SqlQuery = sqlQuery;
        CacheSeconds = cacheSeconds;
    }
}

/// <summary>
/// Command / DTO for updating an existing dataset.
/// </summary>
public record UpdateDatasetDto
{
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public Guid DataConnectionId { get; init; }
    public string SqlQuery { get; init; } = string.Empty;
    public int CacheSeconds { get; init; } = 0;

    public UpdateDatasetDto() {}
    public UpdateDatasetDto(string name, string? description, Guid dataConnectionId, string sqlQuery, int cacheSeconds = 0)
    {
        Name = name;
        Description = description;
        DataConnectionId = dataConnectionId;
        SqlQuery = sqlQuery;
        CacheSeconds = cacheSeconds;
    }
}
