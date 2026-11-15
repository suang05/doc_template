using System.Text.Json.Serialization;

namespace SmkDoc.Application.Modules.TenantManagement.BreakGlass.DTOs;

public sealed record BreakGlassGrantResultDto(
    Guid GrantId,
    Guid TargetProjectId,
    Guid SuperAdminUserId,
    Guid GrantedByUserId,
    string Reason,
    DateTimeOffset GrantedAt,
    DateTimeOffset ExpiresAt,
    DateTimeOffset? RevokedAt,
    bool IsActive,
    [property: JsonIgnore] long Version = 1) : IHasVersion, IHasResourceId
{
    [JsonIgnore]
    public Guid ResourceId => GrantId;
}
