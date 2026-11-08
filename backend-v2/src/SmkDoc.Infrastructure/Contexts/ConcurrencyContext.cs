using SmkDoc.Application.Common.Exceptions;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Domain.Interfaces;

namespace SmkDoc.Infrastructure.Contexts;

/// <summary>
/// Scoped implementation of <see cref="IConcurrencyContext"/> and <see cref="IConcurrencyContextSetter"/>.
/// Holds the sanitized HTTP If-Match precondition token and tracks verification state per request scope.
/// </summary>
public sealed class ConcurrencyContext : IConcurrencyContext, IConcurrencyContextSetter
{
    public string? IfMatch { get; private set; }
    public bool HasIfMatch => !string.IsNullOrWhiteSpace(IfMatch);
    public bool IsWildcard => string.Equals(IfMatch, "*", StringComparison.Ordinal);
    public bool IsVerified { get; private set; }

    public void SetIfMatch(string? rawIfMatch)
    {
        if (string.IsNullOrWhiteSpace(rawIfMatch))
        {
            IfMatch = null;
            return;
        }

        var trimmed = rawIfMatch.Trim();
        IfMatch = trimmed == "*" ? "*" : trimmed.Trim('\"', 'W', '/', ' ');
    }

    public void EnsureMatches(IConcurrencyAware entity, string? alternateToken = null)
    {
        var currentToken = entity.GetConcurrencyToken();
        var effectiveAlternate = alternateToken ?? entity.GetAlternateConcurrencyToken();
        var entityId = ResolveEntityId(entity);
        EnsureTokenMatches(currentToken, entity.GetType().Name, entityId, effectiveAlternate);
    }

    public void EnsureTokenMatches(string currentToken, string entityName, object entityId, string? alternateToken = null)
    {
        if (!HasIfMatch || IsWildcard || IsVerified)
        {
            IsVerified = true;
            return;
        }

        bool matchesPrimary = string.Equals(IfMatch, currentToken, StringComparison.OrdinalIgnoreCase);
        bool matchesAlternate = !string.IsNullOrWhiteSpace(alternateToken) &&
                                string.Equals(IfMatch, alternateToken, StringComparison.OrdinalIgnoreCase);

        if (!matchesPrimary && !matchesAlternate)
        {
            throw new PreconditionFailedException(
                $"Precondition failed for {entityName} '{entityId}'. ETag '{IfMatch}' does not match current state.");
        }

        IsVerified = true;
    }

    private static object ResolveEntityId(IConcurrencyAware entity) =>
        entity switch
        {
            SmkDoc.Domain.Entities.Template t => t.Id.Value,
            SmkDoc.Domain.Entities.Dataset d => d.Id.Value,
            SmkDoc.Domain.Entities.DataConnection c => c.Id.Value,
            SmkDoc.Domain.Entities.Project p => p.Id.Value,
            SmkDoc.Domain.Entities.ApiKey k => k.Id.Value,
            SmkDoc.Domain.Entities.UserProjectRole r => r.UserId.Value,
            _ => string.Empty
        };
}

