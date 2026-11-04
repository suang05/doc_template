namespace SmkDoc.Application.Common.Exceptions;

/// <summary>
/// Thrown when an optimistic concurrency check fails (e.g. If-Match mismatch).
/// Maps to HTTP 412 Precondition Failed in RFC 9457 GlobalExceptionHandler.
/// </summary>
public sealed class PreconditionFailedException : Exception
{
    public string ErrorCode { get; }

    public PreconditionFailedException(string message, string errorCode = "PRECONDITION_FAILED")
        : base(message)
    {
        ErrorCode = errorCode;
    }
}

