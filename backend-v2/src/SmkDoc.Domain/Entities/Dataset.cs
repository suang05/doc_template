namespace SmkDoc.Domain.Entities;

public class Dataset : BaseEntity
{
    public string Name { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public Guid DataConnectionId { get; private set; }
    public string SqlQuery { get; private set; } = string.Empty;
    public int CacheSeconds { get; private set; } = 0;

    // Navigation
    public virtual DataConnection? DataConnection { get; private set; }

    private Dataset() { }

    public Dataset(string name, string? description, Guid dataConnectionId, string sqlQuery, int cacheSeconds = 0)
    {
        Name = name;
        Description = description;
        DataConnectionId = dataConnectionId;
        SqlQuery = sqlQuery;
        CacheSeconds = cacheSeconds;
    }

    public void UpdateDetails(string name, string? description, Guid dataConnectionId, string sqlQuery, int cacheSeconds)
    {
        Name = name;
        Description = description;
        DataConnectionId = dataConnectionId;
        SqlQuery = sqlQuery;
        CacheSeconds = cacheSeconds;
        SetUpdated();
    }
}
