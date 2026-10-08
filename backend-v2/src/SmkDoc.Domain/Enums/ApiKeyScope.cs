using SmkDoc.Domain.Common;

namespace SmkDoc.Domain.Enums;

/// <summary>
/// Smart Enum defining permission scope for an API key.
/// </summary>
public class ApiKeyScope : Enumeration
{
    public static readonly ApiKeyScope ReadOnly = new(0, "ReadOnly");
    public static readonly ApiKeyScope ReadWrite = new(1, "ReadWrite");

    private ApiKeyScope(int id, string name) : base(id, name) { }

    public static ApiKeyScope FromName(string name) => FromDisplayName<ApiKeyScope>(name);
    public static bool TryFromName(string? name, out ApiKeyScope? scope) => TryFromDisplayName(name, out scope);
}
