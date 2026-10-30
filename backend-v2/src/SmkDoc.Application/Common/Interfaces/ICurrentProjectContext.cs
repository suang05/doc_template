using SmkDoc.Domain.Enums;
using SmkDoc.Domain.ValueObjects.Ids;

namespace SmkDoc.Application.Common.Interfaces;

/// <summary>
/// Unified channel-agnostic ambient Single Source of Truth for the active tenant/project
/// and caller metadata in the current execution scope.
/// Hydrated once at the HTTP perimeter whether authenticated via X-API-Key (M2M) or Bearer JWT (Web Studio).
/// </summary>
public interface ICurrentProjectContext
{
    ProjectId ProjectId { get; }
    bool IsResolved { get; }
    string? CallerIdentity { get; }
    UserId? UserId { get; }
    SystemRole? SystemRole { get; }
    ApiKeyId? ApiKeyId { get; }
}

/// <summary>
/// Internal write contract utilized exclusively by perimeter security middleware
/// to hydrate the ambient context once per request lifecycle.
/// </summary>
public interface ICurrentProjectContextSetter
{
    void SetProject(ProjectId projectId, string? callerIdentity = null);

    void SetActor(
        UserId? userId = null,
        SystemRole? systemRole = null,
        ApiKeyId? apiKeyId = null,
        string? callerIdentity = null);
}
