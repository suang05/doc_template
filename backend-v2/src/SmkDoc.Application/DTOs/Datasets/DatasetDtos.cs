namespace SmkDoc.Application.DTOs.Datasets;

public class DatasetDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid DataConnectionId { get; set; }
    public string DataConnectionName { get; set; } = string.Empty;
    public string SqlQuery { get; set; } = string.Empty;
    public int CacheSeconds { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
}

public class CreateDatasetDto
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid DataConnectionId { get; set; }
    public string SqlQuery { get; set; } = string.Empty;
    public int CacheSeconds { get; set; } = 0;
}

public class UpdateDatasetDto
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid DataConnectionId { get; set; }
    public string SqlQuery { get; set; } = string.Empty;
    public int CacheSeconds { get; set; } = 0;
}
