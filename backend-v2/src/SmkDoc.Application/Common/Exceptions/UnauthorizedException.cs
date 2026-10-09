using SmkDoc.Domain.Exceptions;

namespace SmkDoc.Application.Common.Exceptions;

/// <summary>
/// Thrown when authentication or permission authorization fails for an action.
/// Maps to HTTP 401 Unauthorized in GlobalExceptionHandler.
/// </summary>
public class UnauthorizedException : DomainException
{
    public UnauthorizedException(string message, string errorCode = "UNAUTHORIZED", Exception? innerException = null)
        : base(message, errorCode, innerException)
    {
    }
}
