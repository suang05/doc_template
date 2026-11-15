namespace SmkDoc.Application.Modules.TenantManagement.Quotas.Commands.UpsertProjectQuota;

public sealed record UpsertProjectQuotaCommand(
    Guid ProjectId,
    int MaxRequestsPerMinute,
    int MaxConcurrentJobs,
    int MaxBatchItems,
    long MaxStorageBytes,
    int MaxActiveApiKeys,
    bool IsActive = true,
    long ExpectedVersion = 1);
