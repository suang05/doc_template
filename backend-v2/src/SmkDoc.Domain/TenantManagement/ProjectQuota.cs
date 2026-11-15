using SmkDoc.Domain.Common;
using SmkDoc.Domain.Exceptions;
using SmkDoc.Domain.Interfaces;
using SmkDoc.Domain.ValueObjects.Ids;

namespace SmkDoc.Domain.TenantManagement;

/// <summary>
/// Governance policy and rate/resource quota configuration for a single tenant project.
/// Managed in Control Plane by SuperAdmins.
/// </summary>
public sealed class ProjectQuota : AggregateRoot<ProjectQuotaId>, IMustHaveProject
{
    public const int DefaultMaxRequestsPerMinute = 60;
    public const int DefaultMaxConcurrentJobs = 4;
    public const int DefaultMaxBatchItems = 100;
    public const long DefaultMaxStorageBytes = 5L * 1024 * 1024 * 1024;
    public const int DefaultMaxActiveApiKeys = 2;

    public ProjectId ProjectId { get; private set; }
    public int MaxRequestsPerMinute { get; private set; }
    public int MaxConcurrentJobs { get; private set; }
    public int MaxBatchItems { get; private set; }
    public long MaxStorageBytes { get; private set; }
    public int MaxActiveApiKeys { get; private set; }
    public bool IsActive { get; private set; }

    // For EF Core materialization only
    private ProjectQuota() { }

    private ProjectQuota(
        ProjectQuotaId id,
        ProjectId projectId,
        int maxRequestsPerMinute,
        int maxConcurrentJobs,
        int maxBatchItems,
        long maxStorageBytes,
        int maxActiveApiKeys,
        DateTimeOffset now)
        : base(id, now)
    {
        if (projectId.IsEmpty)
        {
            throw new DomainValidationException("ProjectId cannot be empty.");
        }

        ProjectId = projectId;
        MaxRequestsPerMinute = Guard.Positive(maxRequestsPerMinute, nameof(MaxRequestsPerMinute));
        MaxConcurrentJobs = Guard.Positive(maxConcurrentJobs, nameof(MaxConcurrentJobs));
        MaxBatchItems = Guard.Positive(maxBatchItems, nameof(MaxBatchItems));
        MaxStorageBytes = RequirePositiveLong(maxStorageBytes, nameof(MaxStorageBytes));
        MaxActiveApiKeys = Guard.Positive(maxActiveApiKeys, nameof(MaxActiveApiKeys));
        IsActive = true;
    }

    public static ProjectQuota CreateDefault(ProjectId projectId, DateTimeOffset now) =>
        new(
            ProjectQuotaId.New(),
            projectId,
            DefaultMaxRequestsPerMinute,
            DefaultMaxConcurrentJobs,
            DefaultMaxBatchItems,
            DefaultMaxStorageBytes,
            DefaultMaxActiveApiKeys,
            now);

    public static ProjectQuota Create(
        ProjectId projectId,
        int maxRequestsPerMinute,
        int maxConcurrentJobs,
        int maxBatchItems,
        long maxStorageBytes,
        int maxActiveApiKeys,
        DateTimeOffset now) =>
        new(
            ProjectQuotaId.New(),
            projectId,
            maxRequestsPerMinute,
            maxConcurrentJobs,
            maxBatchItems,
            maxStorageBytes,
            maxActiveApiKeys,
            now);

    public void UpdateLimits(
        int maxRequestsPerMinute,
        int maxConcurrentJobs,
        int maxBatchItems,
        long maxStorageBytes,
        int maxActiveApiKeys,
        DateTimeOffset now)
    {
        Guard.Positive(maxRequestsPerMinute, nameof(MaxRequestsPerMinute));
        Guard.Positive(maxConcurrentJobs, nameof(MaxConcurrentJobs));
        Guard.Positive(maxBatchItems, nameof(MaxBatchItems));
        RequirePositiveLong(maxStorageBytes, nameof(MaxStorageBytes));
        Guard.Positive(maxActiveApiKeys, nameof(MaxActiveApiKeys));

        if (MaxRequestsPerMinute == maxRequestsPerMinute &&
            MaxConcurrentJobs == maxConcurrentJobs &&
            MaxBatchItems == maxBatchItems &&
            MaxStorageBytes == maxStorageBytes &&
            MaxActiveApiKeys == maxActiveApiKeys)
        {
            return;
        }

        MaxRequestsPerMinute = maxRequestsPerMinute;
        MaxConcurrentJobs = maxConcurrentJobs;
        MaxBatchItems = maxBatchItems;
        MaxStorageBytes = maxStorageBytes;
        MaxActiveApiKeys = maxActiveApiKeys;
        SetUpdated(now);
    }

    public void Deactivate(DateTimeOffset now)
    {
        if (!IsActive) return;
        IsActive = false;
        SetUpdated(now);
    }

    public void Activate(DateTimeOffset now)
    {
        if (IsActive) return;
        IsActive = true;
        SetUpdated(now);
    }

    private static long RequirePositiveLong(long value, string name) =>
        value > 0
            ? value
            : throw new DomainValidationException($"{name} must be positive.");
}
