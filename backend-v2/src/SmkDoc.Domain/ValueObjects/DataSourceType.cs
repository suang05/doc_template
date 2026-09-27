using SmkDoc.Domain.Common;

namespace SmkDoc.Domain.ValueObjects;

public class DataSourceType : ValueObject
{
    public static readonly DataSourceType Json = new("json");
    public static readonly DataSourceType Sql = new("sql");

    public string Value { get; }

    private DataSourceType(string value)
    {
        Value = value;
    }

    public static DataSourceType FromString(string type)
    {
        if (string.IsNullOrWhiteSpace(type))
            throw new ArgumentException("DataSourceType cannot be null or empty.");

        var normalized = type.ToLowerInvariant();
        return normalized switch
        {
            "json" => Json,
            "sql" => Sql,
            _ => throw new ArgumentException($"Invalid DataSourceType: {type}. Allowed values are 'json' and 'sql'.")
        };
    }

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Value;
    }

    public override string ToString() => Value;
}
