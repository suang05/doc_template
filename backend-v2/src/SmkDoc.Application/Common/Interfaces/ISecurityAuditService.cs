namespace SmkDoc.Application.Common.Interfaces;

/// <summary>
/// Domain-clean port for recording security-critical and domain lifecycle audit events (SOC 2, ISO 27001, OWASP ASVS).
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

    /// <summary>
    /// Records post-commit publication of a template version within a tenant project.
    /// </summary>
    void LogTemplatePublished(Guid projectId, Guid templateId, Guid versionId, int versionNumber);

    /// <summary>
    /// Records post-commit creation of a tracked legal document version.
    /// </summary>
    void LogDocumentVersionCreated(Guid projectId, Guid documentId, int versionNumber, Guid? generationLogId);

    /// <summary>
    /// Records post-commit revocation of an M2M API key.
    /// </summary>
    void LogApiKeyRevoked(Guid projectId, Guid apiKeyId);

    /// <summary>
    /// Records post-commit deactivation of a user account.
    /// </summary>
    void LogUserDeactivated(Guid userId);

    /// <summary>
    /// Records issuance of a time-boxed SuperAdmin break-glass grant.
    /// </summary>
    void LogBreakGlassAccessGranted(Guid grantId, Guid projectId, Guid superAdminUserId, DateTimeOffset expiresAt);

    /// <summary>
    /// Records an audited data-plane access by a SuperAdmin operating under an active break-glass grant.
    /// </summary>
    void LogBreakGlassAccessUsed(Guid userId, Guid projectId, string path);

    /// <summary>
    /// Records explicit revocation of a break-glass grant.
    /// </summary>
    void LogBreakGlassAccessRevoked(Guid grantId, Guid projectId);

    /// <summary>
    /// Records usage of a transitional legacy SHA-256 API key that should be rotated to <c>smk_live_</c>.
    /// </summary>
    void LogLegacyApiKeyUsed(Guid apiKeyId, Guid projectId);

    /// <summary>
    /// Records an outbound connection blocked by the SSRF / egress validator.
    /// </summary>
    void LogEgressDenied(Guid? projectId, string schemeOrProvider, string reasonCategory);
}
