namespace SmkDoc.Domain.Interfaces;

/// <summary>
/// Marker and contract for aggregate roots and standalone entities governed by HTTP optimistic concurrency (If-Match / ETag).
/// Child entities within an aggregate must NOT implement this interface so save interceptors evaluate only the root token.
/// </summary>
public interface IConcurrencyAware
{
    object EntityId { get; }
    DateTimeOffset CreatedAt { get; }
    DateTimeOffset? UpdatedAt { get; }

    string GetConcurrencyToken() =>
        (UpdatedAt ?? CreatedAt).ToUnixTimeMilliseconds().ToString();

    string? GetAlternateConcurrencyToken() => null;
}

