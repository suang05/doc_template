using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Domain.ValueObjects.Ids;

namespace SmkDoc.Infrastructure.Contexts;

/// <summary>
/// Explicit scoped tenant/control-plane context switcher for background workers and outbox dispatchers.
/// Captures and restores prior ambient state deterministically on disposal.
/// </summary>
public sealed class TenantScope(CurrentProjectContext projectContext) : ITenantScope
{
    public IDisposable BeginTenant(ProjectId projectId, string? callerIdentity)
    {
        var previous = projectContext.CaptureSnapshot();
        projectContext.EnterTenantScope(projectId, callerIdentity);
        return new ScopeRestoreHandle(projectContext, previous);
    }

    public IDisposable BeginControlPlane(string? callerIdentity)
    {
        var previous = projectContext.CaptureSnapshot();
        projectContext.EnterControlPlaneScope(callerIdentity);
        return new ScopeRestoreHandle(projectContext, previous);
    }

    private sealed class ScopeRestoreHandle(
        CurrentProjectContext projectContext,
        CurrentProjectContext.TenantContextSnapshot previous) : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            projectContext.RestoreSnapshot(previous);
        }
    }
}
