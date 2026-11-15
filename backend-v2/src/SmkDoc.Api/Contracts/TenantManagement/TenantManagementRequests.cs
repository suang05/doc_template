using System.Text.Json.Serialization;

namespace SmkDoc.Api.Contracts.TenantManagement;

/// <summary>
/// Control-plane request to update tenant-approved outbound egress CIDRs for SQL connections and webhooks.
/// </summary>
public sealed record UpdateProjectEgressCidrsRequest(IReadOnlyList<string> AllowedCidrs);

/// <summary>
/// Control-plane request to create or update per-project operational quotas.
/// </summary>
public sealed record UpsertProjectQuotaRequest(
    [property: JsonRequired] int MaxRequestsPerMinute,
    [property: JsonRequired] int MaxConcurrentJobs,
    [property: JsonRequired] int MaxBatchItems,
    [property: JsonRequired] long MaxStorageBytes,
    [property: JsonRequired] int MaxActiveApiKeys,
    bool IsActive = true);

/// <summary>
/// Control-plane request to grant time-boxed break-glass access for a SuperAdmin into a tenant project.
/// </summary>
public sealed record GrantBreakGlassAccessRequest(
    [property: JsonRequired] Guid TargetProjectId,
    string Reason,
    Guid? SuperAdminUserId = null,
    int? DurationMinutes = 60);
