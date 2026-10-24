using SmkDoc.Domain.Common;
using SmkDoc.Domain.Exceptions;

namespace SmkDoc.Domain.Enums;

/// <summary>
/// Smart Enum defining supported data source origins for template field mappings.
/// </summary>
public sealed class DataSourceType : Enumeration
{
    public static readonly DataSourceType Json = new(1, "json");
    public static readonly DataSourceType Sql = new(2, "sql");

    private DataSourceType(int id, string name) : base(id, name) { }

    public static DataSourceType FromString(string type)
    {
        if (string.IsNullOrWhiteSpace(type))
        {
            throw new DomainValidationException("DataSourceType cannot be null or empty.");
        }

        var normalized = type.ToLowerInvariant();
        return normalized switch
        {
            "json" => Json,
            "sql" => Sql,
            _ => throw new DomainValidationException($"Invalid DataSourceType: {type}. Allowed values are 'json' and 'sql'.")
        };
    }

    public static implicit operator string(DataSourceType type) => type.Name;
    public string Value => Name;
}
