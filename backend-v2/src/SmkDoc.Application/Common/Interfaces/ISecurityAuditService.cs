namespace SmkDoc.Application.Common.Interfaces;

/// <summary>
/// Domain-clean port for recording security-critical audit events and telemetry (SOC 2, ISO 27001, OWASP ASVS).
/// </summary>
public interface ISecurityAuditService
{
    /// <summary>
    /// Records a successful user login with active workspace context.
    /// </summary>
    void LogLoginSuccess(Guid userId, string email, Guid? activeProjectId);

    /// <summary>
    /// Records an authentication failure attempt with deterministic reason.
    /// </summary>
    void LogLoginFailure(string email, string reason);

    /// <summary>
    /// Records successful refresh token rotation.
    /// </summary>
    void LogTokenRotated(Guid userId);

    /// <summary>
    /// Records critical security alert when an expired/revoked refresh token is re-submitted outside the leeway window.
    /// </summary>
    void LogTokenReuseDetected(Guid userId);
}
