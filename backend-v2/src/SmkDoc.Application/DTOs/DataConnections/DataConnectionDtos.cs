namespace SmkDoc.Application.DTOs.DataConnections;

public class DataConnectionDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Provider { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
}

public class CreateDataConnectionDto
{
    public string Name { get; set; } = string.Empty;
    public string Provider { get; set; } = string.Empty;
    public string ConnectionString { get; set; } = string.Empty;
}

public class UpdateDataConnectionDto
{
    public string Name { get; set; } = string.Empty;
    public string Provider { get; set; } = string.Empty;
    public string ConnectionString { get; set; } = string.Empty;
}

public class TestDataConnectionDto
{
    public string Provider { get; set; } = string.Empty;
    public string ConnectionString { get; set; } = string.Empty;
}
