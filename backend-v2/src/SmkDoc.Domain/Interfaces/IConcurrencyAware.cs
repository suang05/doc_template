namespace SmkDoc.Domain.Interfaces;

/// <summary>
/// Marker and contract for aggregate roots and standalone entities governed by explicit optimistic concurrency (If-Match / ETag).
/// Child entities within an aggregate must NOT implement this interface so persistence evaluates only the root version.
/// </summary>
public interface IConcurrencyAware
{
    object EntityId { get; }
    long Version { get; }
}

