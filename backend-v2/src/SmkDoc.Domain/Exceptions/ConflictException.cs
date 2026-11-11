namespace SmkDoc.Domain.Exceptions;

/// <summary>
/// Thrown when a business conflict occurs (e.g. duplicate slug, version, placeholder, or resource collision).
/// Maps to HTTP 409 Conflict at the presentation layer.
/// </summary>
public class ConflictException : DomainException
{
    public ConflictException(string message, string errorCode = "RESOURCE_CONFLICT", Exception? innerException = null)
        : base(message, errorCode, innerException)
    {
    }

    public static ConflictException DuplicateSlug(string slug)
        => new($"Template slug '{slug}' is already in use.", "RESOURCE_CONFLICT");

    public static ConflictException DuplicateVersion(Guid aggregateId, int version)
        => new($"Resource '{aggregateId}' already contains version {version}.", "DUPLICATE_VERSION");

    public static ConflictException DuplicatePlaceholder(Guid templateId, string placeholder)
        => new($"Template '{templateId}' already defines placeholder '{placeholder}'.", "DUPLICATE_PLACEHOLDER");

    public static ConflictException DuplicateDatasetAlias(Guid templateId, string alias)
        => new($"Template '{templateId}' already defines dataset alias '{alias}'.", "DUPLICATE_DATASET_ALIAS");
}

