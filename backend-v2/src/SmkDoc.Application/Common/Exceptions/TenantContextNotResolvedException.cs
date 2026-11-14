namespace SmkDoc.Application.Common.Exceptions;

/// <summary>
/// Thrown when internal application or persistence code requires a resolved ambient tenant context,
/// indicating that perimeter tenant resolution was bypassed.
/// Maps to HTTP 500 Internal Server Error with <see cref="ErrorCodes.TenantContextNotResolved"/>.
/// </summary>
public sealed class TenantContextNotResolvedException : DomainException
{
    public TenantContextNotResolvedException(string? message = null, Exception? innerException = null)
        : base(
            message ?? "Ambient tenant project context was not resolved for this execution path.",
            ErrorCodes.TenantContextNotResolved,
            innerException)
    {
    }

    public static TenantContextNotResolvedException ForOperation(string operation) =>
        new($"Operation '{operation}' requires a resolved ambient tenant project context.");
}
