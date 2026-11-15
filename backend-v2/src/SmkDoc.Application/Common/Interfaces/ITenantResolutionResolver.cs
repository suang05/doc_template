using SmkDoc.Domain.ValueObjects.Ids;

namespace SmkDoc.Application.Common.Interfaces;

/// <summary>
/// Cached perimeter resolver for tenant state, project memberships, user revocation, and break-glass grants.
/// Invalidated via domain events dispatched by the Outbox Relay Worker.
/// </summary>
public interface ITenantResolutionResolver : ITenantResolutionPort
{
    Task<bool> IsUserActiveAsync(UserId userId, CancellationToken ct);
    Task<bool> HasActiveBreakGlassGrantAsync(UserId superAdminUserId, ProjectId projectId, DateTimeOffset now, CancellationToken ct);
    Task InvalidateProjectAsync(ProjectId projectId, CancellationToken ct);
    Task InvalidateUserAsync(UserId userId, CancellationToken ct);
    Task InvalidateBreakGlassAsync(UserId superAdminUserId, ProjectId projectId, CancellationToken ct);
}
