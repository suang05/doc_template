namespace SmkDoc.Domain.Exceptions;

/// <summary>
/// Thrown when attempting to add a document version number that already exists within the document aggregate.
/// </summary>
public sealed class DuplicateDocumentVersionException : BusinessRuleViolationException
{
    public DuplicateDocumentVersionException(Guid documentId, int version)
        : base($"Version {version} already exists in document '{documentId}'.", "DUPLICATE_DOCUMENT_VERSION")
    {
    }
}
