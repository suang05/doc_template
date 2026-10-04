namespace SmkDoc.Domain.Exceptions;

/// <summary>
/// Thrown when entity or value object invariants or input parameters violate domain business constraints.
/// </summary>
public sealed class DomainValidationException : DomainException
{
    public DomainValidationException(string message, Exception? innerException = null)
        : base(message, "DOMAIN_VALIDATION_ERROR", innerException)
    {
    }
}
