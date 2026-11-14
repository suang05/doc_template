namespace SmkDoc.Application.Common.Interfaces;

/// <summary>
/// Ambient request-scoped optimistic concurrency state resolved from the HTTP If-Match header.
/// </summary>
public interface IConcurrencyContext
{
    string? IfMatch { get; set; }
    bool IsVerified { get; }

    void EnsureMatches(IConcurrencyAware entity, string? alternateToken = null);
    void EnsureTokenMatches(string currentToken, string entityName, object entityId, string? alternateToken = null);
}

