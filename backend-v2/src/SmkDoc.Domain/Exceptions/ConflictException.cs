namespace SmkDoc.Domain.Exceptions;

/// <summary>
/// Thrown when a business conflict occurs (e.g. duplicate slug, resource already exists).
/// Maps to HTTP 409 Conflict at the presentation layer.
/// </summary>
public class ConflictException : DomainException
{
    public ConflictException(string message, Exception? innerException = null)
        : base(message, "RESOURCE_CONFLICT", innerException)
    {
    }

    public static ConflictException DuplicateSlug(string slug)
        => new($"Template slug '{slug}' is already in use.");
}
