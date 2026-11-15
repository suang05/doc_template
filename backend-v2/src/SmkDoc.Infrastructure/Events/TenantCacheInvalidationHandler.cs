using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Domain.Events;

namespace SmkDoc.Infrastructure.Events;

/// <summary>
/// Synchronizes perimeter caches across API replicas whenever project activation, user status,
/// or break-glass grant lifecycle events are relayed through the Outbox.
/// </summary>
public sealed class TenantCacheInvalidationHandler(
    ITenantResolutionResolver resolver) :
    IDomainEventHandler<ProjectActivatedDomainEvent>,
    IDomainEventHandler<ProjectDeactivatedDomainEvent>,
    IDomainEventHandler<BreakGlassGrantedDomainEvent>,
    IDomainEventHandler<BreakGlassRevokedDomainEvent>,
    IDomainEventHandler<UserDeactivatedDomainEvent>
{
    public Task HandleAsync(ProjectActivatedDomainEvent domainEvent, CancellationToken ct) =>
        resolver.InvalidateProjectAsync(domainEvent.ProjectId, ct);

    public Task HandleAsync(ProjectDeactivatedDomainEvent domainEvent, CancellationToken ct) =>
        resolver.InvalidateProjectAsync(domainEvent.ProjectId, ct);

    public Task HandleAsync(BreakGlassGrantedDomainEvent domainEvent, CancellationToken ct) =>
        resolver.InvalidateBreakGlassAsync(domainEvent.SuperAdminUserId, domainEvent.TargetProjectId, ct);

    public Task HandleAsync(BreakGlassRevokedDomainEvent domainEvent, CancellationToken ct) =>
        resolver.InvalidateBreakGlassAsync(domainEvent.SuperAdminUserId, domainEvent.TargetProjectId, ct);

    public Task HandleAsync(UserDeactivatedDomainEvent domainEvent, CancellationToken ct) =>
        resolver.InvalidateUserAsync(domainEvent.UserId, ct);
}
