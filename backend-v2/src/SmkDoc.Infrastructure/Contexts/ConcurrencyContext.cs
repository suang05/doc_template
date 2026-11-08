using SmkDoc.Application.Common.Exceptions;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Domain.Interfaces;

namespace SmkDoc.Infrastructure.Contexts;

/// <summary>
/// Scoped implementation of <see cref="IConcurrencyContext"/>.
/// Automatically sanitizes the HTTP If-Match precondition token and tracks verification state per request.
/// </summary>
public sealed class ConcurrencyContext : IConcurrencyContext
{
    private string? _ifMatch;

    public string? IfMatch
    {
        get => _ifMatch;
        set => _ifMatch = string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim() is "*" ? "*" : value.Trim('\"', 'W', '/', ' ');
    }

    public bool IsVerified { get; private set; }

    public void EnsureMatches(IConcurrencyAware entity, string? alternateToken = null) =>
        EnsureTokenMatches(
            entity.GetConcurrencyToken(),
            entity.GetType().Name,
            entity.EntityId,
            alternateToken ?? entity.GetAlternateConcurrencyToken());

    public void EnsureTokenMatches(string currentToken, string entityName, object entityId, string? alternateToken = null)
    {
        if (string.IsNullOrWhiteSpace(_ifMatch) || _ifMatch == "*" || IsVerified)
        {
            IsVerified = true;
            return;
        }

        if (!string.Equals(_ifMatch, currentToken, StringComparison.OrdinalIgnoreCase) &&
            (string.IsNullOrWhiteSpace(alternateToken) || !string.Equals(_ifMatch, alternateToken, StringComparison.OrdinalIgnoreCase)))
        {
            throw new PreconditionFailedException(
                $"Precondition failed for {entityName} '{entityId}'. ETag '{_ifMatch}' does not match current state.");
        }

        IsVerified = true;
    }
}

