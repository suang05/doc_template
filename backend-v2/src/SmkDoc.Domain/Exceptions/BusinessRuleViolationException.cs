namespace SmkDoc.Domain.Exceptions;

/// <summary>
/// Thrown when an operation violates an explicit domain business rule or invalid entity lifecycle transition.
/// </summary>
public class BusinessRuleViolationException : DomainException
{
    public BusinessRuleViolationException(string message, string errorCode = ErrorCodes.BusinessRuleViolation, Exception? innerException = null)
        : base(message, errorCode, innerException)
    {
    }
}
