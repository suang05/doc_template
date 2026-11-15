
namespace SmkDoc.Application.Common.Exceptions;

/// <summary>
/// Coarse security boundary violation exception emitted when SSRF, egress policy, or tenant isolation checks reject a request
/// without leaking internal network topology, DNS failures, or resolved IP addresses.
/// </summary>
public sealed class SecurityViolationException : Exception
{
    public string ErrorCode { get; }

    public SecurityViolationException(string message, string errorCode = ErrorCodes.EgressDenied)
        : base(message)
    {
        ErrorCode = errorCode;
    }

    public static SecurityViolationException EgressDenied() =>
        new("Outbound connection target is denied by egress security policy.", ErrorCodes.EgressDenied);

    public static SecurityViolationException BreakGlassRequired() =>
        new("SuperAdmin data-plane access requires an explicit project role or active break-glass grant.", ErrorCodes.CrossTenantAccessDenied);
}
