using SmkDoc.Domain.ValueObjects.Ids;

namespace SmkDoc.Domain.Exceptions;

/// <summary>
/// Domain exception thrown when an authenticated caller attempts cross-tenant access to a resource belonging to another tenant.
/// Maps to RFC 9457 HTTP 403 Forbidden with stable ErrorCode "CROSS_TENANT_ACCESS_DENIED".
/// </summary>
public sealed class CrossTenantAccessDeniedException : DomainException
{
    public const string DefaultErrorCode = ErrorCodes.CrossTenantAccessDenied;

    public CrossTenantAccessDeniedException(ProjectId resourceProjectId)
        : base($"Access denied: caller does not have permission to access resources in project '{resourceProjectId.Value}'.",
               DefaultErrorCode)
    {
    }

    public CrossTenantAccessDeniedException(Guid resourceProjectId)
        : base($"Access denied: caller does not have permission to access resources in project '{resourceProjectId}'.",
               DefaultErrorCode)
    {
    }
}
