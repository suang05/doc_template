using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using SmkDoc.Domain.ValueObjects.Ids;

namespace SmkDoc.Infrastructure.Persistence.Converters;

/// <summary>
/// Centralized EF Core value converters for strongly-typed domain value objects and identifiers.
/// </summary>
public static class DomainValueConverters
{
    public static readonly ValueConverter<ProjectId, Guid> ProjectIdConverter =
        new(id => id.Value, v => new ProjectId(v));
}
