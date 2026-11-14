namespace SmkDoc.Application.Common.Exceptions;

/// <summary>
/// Represents a recoverable infrastructure failure when communicating with an external backing service
/// (for example, MinIO object storage, Redis, or Gotenberg).
/// </summary>
public sealed class ExternalServiceException(
    string serviceName,
    string message,
    Exception? innerException = null) : Exception(message, innerException)
{
    public string ServiceName { get; } = serviceName;

    public string ErrorCode => ErrorCodes.ExternalServiceUnavailable;
}
