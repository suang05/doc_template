using SmkDoc.Domain.ValueObjects.Ids;

namespace SmkDoc.Application.Common.Interfaces;

/// <summary>
/// Explicit async-safe ambient tenant / control-plane scope switcher for background workers, outbox relay,
/// and cross-tenant governance operations.
/// </summary>
public interface ITenantScope
{
    IDisposable BeginTenant(ProjectId projectId, string? callerIdentity);
    IDisposable BeginControlPlane(string? callerIdentity);
}
