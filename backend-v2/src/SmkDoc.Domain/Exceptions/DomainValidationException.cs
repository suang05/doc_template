namespace SmkDoc.Domain.Exceptions;

/// <summary>
/// Thrown when entity or value object invariants or input parameters violate domain business constraints.
/// </summary>
public class DomainValidationException : DomainException
{
    public DomainValidationException(string message, string errorCode = "DOMAIN_VALIDATION_ERROR", Exception? innerException = null)
        : base(message, errorCode, innerException)
    {
    }
}
