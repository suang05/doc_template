using SmkDoc.Domain.Exceptions;

namespace SmkDoc.Domain.Exceptions;

/// <summary>
/// Domain exception thrown when an operation strictly requires an active, resolved tenant project context.
/// Maps to RFC 9457 HTTP 403 Forbidden with stable ErrorCode "TENANT_CONTEXT_REQUIRED".
/// </summary>
public sealed class TenantContextRequiredException : DomainException
{
    public const string DefaultErrorCode = "TENANT_CONTEXT_REQUIRED";

    public TenantContextRequiredException(string? operation = null)
        : base(
            operation is null
                ? "An active tenant project context is required for this operation."
                : $"Operation '{operation}' requires an active tenant project context.",
            DefaultErrorCode)
    {
    }

    public static TenantContextRequiredException ForOperation(string operation) => new(operation);
}
