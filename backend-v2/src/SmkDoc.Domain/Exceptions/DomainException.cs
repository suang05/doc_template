namespace SmkDoc.Domain.Exceptions;

/// <summary>
/// Abstract base class for all domain and business rule exceptions.
/// Enables the presentation layer to distinguish expected business faults from unexpected technical errors.
/// </summary>
public abstract class DomainException : Exception
{
    /// <summary>
    /// Machine-readable unique error code (e.g. "RESOURCE_NOT_FOUND", "DRAFT_EXPIRED").
    /// </summary>
    public string ErrorCode { get; }

    protected DomainException(string message, string errorCode, Exception? innerException = null)
        : base(message, innerException)
    {
        ErrorCode = errorCode;
    }
}
