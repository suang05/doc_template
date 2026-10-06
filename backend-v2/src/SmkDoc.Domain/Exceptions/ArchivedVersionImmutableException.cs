namespace SmkDoc.Domain.Exceptions;

/// <summary>
/// Thrown when attempting to modify an archived template version.
/// </summary>
public sealed class ArchivedVersionImmutableException(Guid versionId, string operation)
    : BusinessRuleViolationException($"Cannot perform '{operation}' on archived template version '{versionId}'.", "ARCHIVED_VERSION_IMMUTABLE")
{
    public Guid VersionId { get; } = versionId;
    public string Operation { get; } = operation;
}
