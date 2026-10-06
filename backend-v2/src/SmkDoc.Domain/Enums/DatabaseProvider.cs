using SmkDoc.Domain.Common;

namespace SmkDoc.Domain.Enums;

/// <summary>
/// Smart Enum representing supported external database connection providers.
/// </summary>
public sealed class DatabaseProvider : Enumeration
{
    public static readonly DatabaseProvider PostgreSQL = new(1, "PostgreSQL");
    public static readonly DatabaseProvider SqlServer = new(2, "SqlServer");
    public static readonly DatabaseProvider MySQL = new(3, "MySQL");
    public static readonly DatabaseProvider Oracle = new(4, "Oracle");

    private DatabaseProvider(int id, string name) : base(id, name) { }
}
