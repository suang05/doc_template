namespace SmkDoc.Application.Common.Exceptions;

/// <summary>
/// Thrown when a fail-closed distributed state store (e.g., Redis idempotency lock) is unreachable.
/// Maps to HTTP 503 Service Unavailable in GlobalExceptionHandler so clients retry safely without duplicate mutations.
/// </summary>
public sealed class DistributedStateUnavailableException(
    string message,
    Exception? innerException = null) : Exception(message, innerException)
{
    public string ErrorCode { get; } = ErrorCodes.ExternalServiceUnavailable;
}
