using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Domain.Enums;
using SmkDoc.Domain.Exceptions;
using SmkDoc.Domain.ValueObjects.Ids;

namespace SmkDoc.Infrastructure.Contexts;

/// <summary>
/// Scoped implementation of <see cref="ICurrentProjectContext"/> and <see cref="ICurrentProjectContextSetter"/>.
/// Provides immutable guarantees once a project has been successfully resolved for the request.
/// </summary>
public sealed class CurrentProjectContext : ICurrentProjectContext, ICurrentProjectContextSetter
{
    public ProjectId ProjectId { get; private set; } = ProjectId.Empty;
    public bool IsResolved => !ProjectId.IsEmpty;
    public string? CallerIdentity { get; private set; }
    public UserId? UserId { get; private set; }
    public SystemRole? SystemRole { get; private set; }
    public ApiKeyId? ApiKeyId { get; private set; }

    public void SetProject(ProjectId projectId, string? callerIdentity = null)
    {
        if (IsResolved)
        {
            throw new InvalidOperationException("Project context has already been resolved and is immutable for this request.");
        }

        if (projectId.IsEmpty)
        {
            throw new DomainValidationException("ProjectId cannot be empty.");
        }

        ProjectId = projectId;
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
}
