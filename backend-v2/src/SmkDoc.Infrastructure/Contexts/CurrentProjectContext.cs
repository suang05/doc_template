using SmkDoc.Application.Common.Exceptions;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Domain.Enums;
using SmkDoc.Domain.Exceptions;
using SmkDoc.Domain.ValueObjects.Ids;

namespace SmkDoc.Infrastructure.Contexts;

/// <summary>
/// Scoped implementation of <see cref="ICurrentProjectContext"/> and <see cref="ICurrentProjectContextSetter"/>.
/// Immutable for normal HTTP request processing once resolved; supports controlled snapshot switching
/// exclusively via <see cref="TenantScope"/> for background workers.
/// </summary>
public sealed class CurrentProjectContext : ICurrentProjectContext, ICurrentProjectContextSetter
{
    private ProjectId _projectId = ProjectId.Empty;

    public ProjectId GetRequiredProjectId() => IsResolved
        ? _projectId
        : throw new TenantContextNotResolvedException();

    public ProjectId? TryGetProjectId() => IsResolved ? _projectId : null;

    public bool IsResolved => !_projectId.IsEmpty;
    public bool IsControlPlane { get; private set; }
    public string? CallerIdentity { get; private set; }
    public UserId? UserId { get; private set; }
    public SystemRole? SystemRole { get; private set; }
    public ApiKeyId? ApiKeyId { get; private set; }

    public void SetProject(ProjectId projectId, string? callerIdentity = null)
    {
        if (IsResolved || IsControlPlane)
        {
            throw new InvalidOperationException("Project context has already been resolved and is immutable for this request.");
        }

        if (projectId.IsEmpty)
        {
            throw new DomainValidationException("ProjectId cannot be empty.");
        }

        _projectId = projectId;
        IsControlPlane = false;
        if (!string.IsNullOrWhiteSpace(callerIdentity))
        {
            CallerIdentity = callerIdentity;
        }
    }

    public void SetControlPlane(string? callerIdentity = null)
    {
        if (IsResolved)
        {
            throw new InvalidOperationException("Cannot enter Control Plane mode when a tenant ProjectId is already active on this request.");
        }

        IsControlPlane = true;
        if (!string.IsNullOrWhiteSpace(callerIdentity))
        {
            CallerIdentity = callerIdentity;
        }
    }

    public void SetActor(
        UserId? userId = null,
        SystemRole? systemRole = null,
        ApiKeyId? apiKeyId = null,
        string? callerIdentity = null)
    {
        UserId = userId;
        SystemRole = systemRole;
        ApiKeyId = apiKeyId;
        if (!string.IsNullOrWhiteSpace(callerIdentity))
        {
            CallerIdentity = callerIdentity;
        }
    }

    internal TenantContextSnapshot CaptureSnapshot() =>
        new(_projectId, IsControlPlane, CallerIdentity);

    internal void RestoreSnapshot(TenantContextSnapshot snapshot)
    {
        _projectId = snapshot.ProjectId;
        IsControlPlane = snapshot.IsControlPlane;
        CallerIdentity = snapshot.CallerIdentity;
    }

    internal void EnterTenantScope(ProjectId projectId, string? callerIdentity)
    {
        if (projectId.IsEmpty)
        {
            throw new DomainValidationException("ProjectId cannot be empty.");
        }

        _projectId = projectId;
        IsControlPlane = false;
        if (!string.IsNullOrWhiteSpace(callerIdentity))
        {
            CallerIdentity = callerIdentity;
        }
    }

    internal void EnterControlPlaneScope(string? callerIdentity)
    {
        _projectId = ProjectId.Empty;
        IsControlPlane = true;
        if (!string.IsNullOrWhiteSpace(callerIdentity))
        {
            CallerIdentity = callerIdentity;
        }
    }

    internal readonly record struct TenantContextSnapshot(
        ProjectId ProjectId,
        bool IsControlPlane,
        string? CallerIdentity);
}
