using SmkDoc.Domain.ValueObjects.Ids;

namespace SmkDoc.Application.Common.Interfaces;

public sealed record ProjectQuotaSnapshot(
    int MaxRequestsPerMinute,
    int MaxConcurrentJobs,
    int MaxBatchItems,
    long MaxStorageBytes,
    int MaxActiveApiKeys);

/// <summary>
/// Read-only Open Host Service port resolving per-tenant rate, batch, storage, and credential quotas.
/// </summary>
public interface IQuotaReader
{
    Task<ProjectQuotaSnapshot> GetQuotaForProjectAsync(ProjectId projectId, CancellationToken ct);
}
