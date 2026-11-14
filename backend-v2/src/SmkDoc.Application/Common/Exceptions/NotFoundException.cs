namespace SmkDoc.Application.Common.Exceptions;

/// <summary>
/// Thrown when a requested entity or resource is not found (or hidden due to tenant isolation).
/// Maps to HTTP 404 Not Found at the presentation layer.
/// </summary>
public class NotFoundException : DomainException
{
    public NotFoundException(string message, Exception? innerException = null)
        : base(message, ErrorCodes.NotFound, innerException)
    {
    }

    public NotFoundException(string name, object key, Exception? innerException = null)
        : base($"Entity \"{name}\" ({key}) was not found.", ErrorCodes.NotFound, innerException)
    {
    }
}
