using System.Text.Json.Serialization;

namespace SmkDoc.Application.Modules.TenantManagement.Fonts.DTOs;

public sealed record PlatformFontResultDto(
    Guid Id,
    string FamilyName,
    string Weight,
    string Style,
    string Format,
    string StorageKey,
    string ChecksumSha256,
    bool IsSystemFont,
    Guid? ProjectId,
    DateTimeOffset CreatedAt,
    [property: JsonIgnore] long Version = 1) : IHasVersion, IHasResourceId
{
    [JsonIgnore]
    public Guid ResourceId => Id;
}
