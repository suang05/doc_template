namespace SmkDoc.Domain.Exceptions;

/// <summary>
/// Thrown when a requested entity or resource is not found.
/// Maps to HTTP 404 Not Found at the presentation layer.
/// </summary>
public class NotFoundException : DomainException
{
    public NotFoundException(string message, Exception? innerException = null) 
        : base(message, "RESOURCE_NOT_FOUND", 404, innerException)
    {
    }

    public NotFoundException(string name, object key, Exception? innerException = null) 
        : base($"Entity \"{name}\" ({key}) was not found.", "RESOURCE_NOT_FOUND", 404, innerException)
    {
    }
}
