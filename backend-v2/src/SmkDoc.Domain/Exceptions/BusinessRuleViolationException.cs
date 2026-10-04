namespace SmkDoc.Domain.Exceptions;

/// <summary>
/// Thrown when an operation violates an explicit domain business rule or invalid entity lifecycle transition.
/// </summary>
public sealed class BusinessRuleViolationException : DomainException
{
    public BusinessRuleViolationException(string message, string errorCode = "BUSINESS_RULE_VIOLATION", Exception? innerException = null)
        : base(message, errorCode, innerException)
    {
    }
}
