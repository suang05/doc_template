using SmkDoc.Domain.Common;
using SmkDoc.Domain.Exceptions;

namespace SmkDoc.Domain.ValueObjects;

public sealed record class DataSourceType
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
            throw new DomainValidationException("DataSourceType cannot be null or empty.");

        var normalized = type.ToLowerInvariant();
        return normalized switch
        {
            "json" => Json,
            "sql" => Sql,
            _ => throw new DomainValidationException($"Invalid DataSourceType: {type}. Allowed values are 'json' and 'sql'.")
        };
    }

    public override string ToString() => Value;
}
