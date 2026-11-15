using System.Text.Json.Serialization;

namespace SmkDoc.Application.Modules.IdentityAccess.Users.DTOs;

/// <summary>
/// DTO representing user membership in a project.
/// </summary>
public sealed record UserResultDto(
    Guid Id,
    string Email,
    string FirstName,
    string LastName,
    string Role,
    bool IsActive,
    DateTimeOffset CreatedAt,
    [property: JsonIgnore] long Version = 1
) : IHasVersion, IHasResourceId
{
    [JsonIgnore]
    public Guid ResourceId => Id;
}
