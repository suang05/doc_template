namespace SmkDoc.Application.DTOs.DataConnections;

/// <summary>
/// DTO representing a safe database connection definition (connection string omitted).
/// </summary>
public record DataConnectionDto(
    Guid Id,
    string Name,
    string Provider,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt
)
{
    public DataConnectionDto() : this(Guid.Empty, string.Empty, string.Empty, default, null) {}
    public Guid Id { get; init; } = Id;
    public string Name { get; init; } = Name;
    public string Provider { get; init; } = Provider;
    public DateTimeOffset CreatedAt { get; init; } = CreatedAt;
    public DateTimeOffset? UpdatedAt { get; init; } = UpdatedAt;
}

/// <summary>
/// Command / DTO for creating a new database connection.
/// </summary>
public record CreateDataConnectionDto
{
    public string Name { get; init; } = string.Empty;
    public string Provider { get; init; } = string.Empty;
    public string ConnectionString { get; init; } = string.Empty;

    public CreateDataConnectionDto() {}
    public CreateDataConnectionDto(string name, string provider, string connectionString)
    {
        Name = name;
        Provider = provider;
        ConnectionString = connectionString;
    }
}

/// <summary>
/// Command / DTO for updating an existing database connection.
/// </summary>
public record UpdateDataConnectionDto
{
    public string Name { get; init; } = string.Empty;
    public string Provider { get; init; } = string.Empty;
    public string ConnectionString { get; init; } = string.Empty;

    public UpdateDataConnectionDto() {}
    public UpdateDataConnectionDto(string name, string provider, string connectionString)
    {
        Name = name;
        Provider = provider;
        ConnectionString = connectionString;
    }
}

/// <summary>
/// Command / DTO for testing database connection credentials.
/// </summary>
public record TestDataConnectionDto
{
    public string Provider { get; init; } = string.Empty;
    public string ConnectionString { get; init; } = string.Empty;

    public TestDataConnectionDto() {}
    public TestDataConnectionDto(string provider, string connectionString)
    {
        Provider = provider;
        ConnectionString = connectionString;
    }
}
