using System.Text.Json.Serialization;

namespace SmkDoc.Application.Modules.TenantManagement.Quotas.DTOs;

public sealed record ProjectQuotaResultDto(
    Guid QuotaId,
    Guid ProjectId,
    int MaxRequestsPerMinute,
    int MaxConcurrentJobs,
    int MaxBatchItems,
    long MaxStorageBytes,
    int MaxActiveApiKeys,
    bool IsActive,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt,
    [property: JsonIgnore] long Version = 1) : IHasVersion, IHasResourceId
{
    [JsonIgnore]
    public Guid ResourceId => QuotaId;
}
