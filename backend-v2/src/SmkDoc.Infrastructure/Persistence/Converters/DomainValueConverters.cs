using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using SmkDoc.Domain.ValueObjects;
using SmkDoc.Domain.ValueObjects.Ids;

namespace SmkDoc.Infrastructure.Persistence.Converters;

/// <summary>
/// Centralized EF Core value converters for strongly-typed domain value objects and identifiers.
/// </summary>
public static class DomainValueConverters
{
    public static readonly ValueConverter<ProjectId, Guid> ProjectIdConverter =
        new(id => id.Value, v => new ProjectId(v));

    public static readonly ValueConverter<EncryptedConnectionString, string> EncryptedConnectionStringConverter =
        new(cs => cs.Value, v => EncryptedConnectionString.Create(v));

    public static readonly ValueConverter<SqlQuery, string> SqlQueryConverter =
        new(q => q.Value, v => SqlQuery.Create(v));

    public static readonly ValueConverter<ProjectSlug, string> ProjectSlugConverter =
        new(s => s.Value, v => new ProjectSlug(v));
}
