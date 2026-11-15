using SmkDoc.Domain.ValueObjects.Ids;

namespace SmkDoc.Application.Common.Interfaces;

/// <summary>
/// Read-only port checking whether a SuperAdmin has an active, unrevoked break-glass grant
/// authorizing data-plane access to a specific tenant project.
/// </summary>
public interface IBreakGlassGrantReader
{
    Task<bool> HasActiveGrantAsync(UserId superAdminUserId, ProjectId targetProjectId, DateTimeOffset now, CancellationToken ct);
}
