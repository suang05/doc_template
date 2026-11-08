using SmkDoc.Domain.Interfaces;

namespace SmkDoc.Application.Common.Interfaces;

/// <summary>
/// Ambient request-scoped optimistic concurrency state resolved from the HTTP If-Match header.
/// </summary>
public interface IConcurrencyContext
{
    string? IfMatch { get; }
    bool HasIfMatch { get; }
    bool IsWildcard { get; }
    bool IsVerified { get; }

    void EnsureMatches(IConcurrencyAware entity, string? alternateToken = null);
    void EnsureTokenMatches(string currentToken, string entityName, object entityId, string? alternateToken = null);
}

/// <summary>
/// Internal write contract used by ETagFilter to hydrate the ambient If-Match token once per request.
/// </summary>
public interface IConcurrencyContextSetter
{
    void SetIfMatch(string? rawIfMatch);
}

