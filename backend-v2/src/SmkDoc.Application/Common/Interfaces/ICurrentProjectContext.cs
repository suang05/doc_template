using SmkDoc.Domain.ValueObjects.Ids;

namespace SmkDoc.Application.Common.Interfaces;

/// <summary>
/// Ambient Single Source of Truth for the active tenant/project in the current execution scope.
/// Registered as Scoped DI per HTTP request or background unit of work.
/// </summary>
public interface ICurrentProjectContext
{
    ProjectId ProjectId { get; }
    bool IsResolved { get; }
    string? CallerIdentity { get; }
}

/// <summary>
/// Internal write contract utilized exclusively by perimeter security middleware
/// to hydrate the project context once per request lifecycle.
/// </summary>
public interface ICurrentProjectContextSetter
{
    void SetProject(ProjectId projectId, string? callerIdentity = null);
}
