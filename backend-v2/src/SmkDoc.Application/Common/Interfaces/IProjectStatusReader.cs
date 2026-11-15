using SmkDoc.Domain.ValueObjects.Ids;

namespace SmkDoc.Application.Common.Interfaces;

public sealed record ProjectStatusSnapshot(
    ProjectId ProjectId,
    string Slug,
    bool IsActive,
    IReadOnlyList<string> AllowedEgressCidrs);

/// <summary>
/// Read-only Open Host Service port exposing project status and per-tenant approved egress CIDRs
/// across Bounded Context boundaries without leaking TenantManagement domain entities.
/// </summary>
public interface IProjectStatusReader
{
    Task<ProjectStatusSnapshot?> FindProjectStatusAsync(ProjectId projectId, CancellationToken ct);
}
