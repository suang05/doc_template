using System.Text.Json.Serialization;

namespace SmkDoc.Application.Modules.TenantManagement.Projects.DTOs;

public sealed record ProjectControlPlaneResultDto(
    Guid Id,
    Guid CompanyId,
    string Name,
    string Slug,
    bool IsActive,
    IReadOnlyList<string> AllowedEgressCidrs,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt,
    [property: JsonIgnore] long Version = 1) : IHasVersion, IHasResourceId
{
    [JsonIgnore]
    public Guid ResourceId => Id;
}
