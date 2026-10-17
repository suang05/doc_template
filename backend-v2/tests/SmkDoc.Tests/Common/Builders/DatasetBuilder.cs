using SmkDoc.Domain.Entities;
using SmkDoc.Domain.ValueObjects;

namespace SmkDoc.Tests.Common.Builders;

public class DatasetBuilder
{
    private Guid _id = Guid.NewGuid();
    private string _name = "Default Dataset";
    private string? _description;
    private Guid _dataConnectionId = Guid.NewGuid();
    private string _sqlQuery = "SELECT 1";
    private int _cacheSeconds = 0;
    private DateTimeOffset _now = TestConstants.BaselineTime;

    public static DatasetBuilder ADataset() => new();

    public static Dataset Default() => ADataset().Build();

    public static Dataset Create(
        Guid? id = null,
        string name = "Default Dataset",
        string? description = null,
        Guid? dataConnectionId = null,
        string sqlQuery = "SELECT 1",
        int cacheSeconds = 0,
        DateTimeOffset? now = null)
    {
        var builder = new DatasetBuilder();
        if (id.HasValue) builder.WithId(id.Value);
        builder.WithName(name);
        if (description != null) builder.WithDescription(description);
        if (dataConnectionId.HasValue) builder.WithDataConnectionId(dataConnectionId.Value);
        builder.WithSqlQuery(sqlQuery);
        builder.WithCacheSeconds(cacheSeconds);
        if (now.HasValue) builder.WithTime(now.Value);
        return builder.Build();
    }

    public DatasetBuilder WithId(Guid id)
    {
        _id = id;
        return this;
    }

    public DatasetBuilder WithName(string name)
    {
        _name = name;
        return this;
    }

    public DatasetBuilder WithDescription(string? description)
    {
        _description = description;
        return this;
    }

    public DatasetBuilder WithDataConnectionId(Guid connectionId)
    {
        _dataConnectionId = connectionId;
        return this;
    }

    public DatasetBuilder WithSqlQuery(string sql)
    {
        _sqlQuery = sql;
        return this;
    }

    public DatasetBuilder WithCacheSeconds(int cacheSeconds)
    {
        _cacheSeconds = cacheSeconds;
        return this;
    }

    public DatasetBuilder WithTime(DateTimeOffset now)
    {
        _now = now;
        return this;
    }

    public Dataset Build()
    {
        return new Dataset(
            _id,
            DatasetName.Create(_name),
            _description,
            _dataConnectionId,
            _sqlQuery,
            _cacheSeconds,
            _now);
    }
}
