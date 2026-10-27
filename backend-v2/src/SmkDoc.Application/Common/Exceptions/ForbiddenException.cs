using SmkDoc.Domain.Exceptions;

namespace SmkDoc.Application.Common.Exceptions;

/// <summary>
/// Thrown when an authenticated caller is denied access or attempts cross-tenant access.
/// Maps to HTTP 403 Forbidden in GlobalExceptionHandler.
/// </summary>
public class ForbiddenException : DomainException
{
    public ForbiddenException(string message, string errorCode = "FORBIDDEN", Exception? innerException = null)
        : base(message, errorCode, innerException)
    {
    }
}
