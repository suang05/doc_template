using Microsoft.Extensions.Logging;
using SmkDoc.Application.Common.Interfaces;

namespace SmkDoc.Infrastructure.Security;

/// <summary>
/// Infrastructure adapter implementing security and domain lifecycle audit logging via Microsoft.Extensions.Logging (Serilog / SIEM).
/// </summary>
public sealed class SecurityAuditService(ILogger<SecurityAuditService> logger) : ISecurityAuditService
{
    public void LogLoginSuccess(Guid userId, string email, Guid? activeProjectId)
    {
        logger.LogInformation(
            "Security Audit: User {UserId} ({Email}) authenticated successfully with active project {ProjectId}",
            userId,
            email,
            activeProjectId);
    }

    public void LogLoginFailure(string email, string reason)
    {
        logger.LogWarning(
            "Security Audit: Authentication failed for {Email}. Reason: {Reason}",
            email,
            reason);
    }

    public void LogTokenRotated(Guid userId)
    {
        logger.LogInformation(
            "Security Audit: Refresh token rotated successfully for user {UserId}",
            userId);
    }

    public void LogTokenReuseDetected(Guid userId)
    {
        logger.LogCritical(
            "Security Alert: Refresh token reuse detected for user {UserId}. Nuclear session revocation executed.",
            userId);
    }

    public void LogTemplatePublished(Guid projectId, Guid templateId, Guid versionId, int versionNumber)
    {
        logger.LogInformation(
            "Domain Audit: Published template {TemplateId} version {VersionNumber} ({VersionId}) in project {ProjectId}",
            templateId,
            versionNumber,
            versionId,
            projectId);
    }

    public void LogDocumentVersionCreated(Guid projectId, Guid documentId, int versionNumber, Guid? generationLogId)
    {
        logger.LogInformation(
            "Domain Audit: Recorded document {DocumentId} version {VersionNumber} (GenerationLogId={GenerationLogId}) in project {ProjectId}",
            documentId,
            versionNumber,
            generationLogId,
            projectId);
    }

    public void LogApiKeyRevoked(Guid projectId, Guid apiKeyId)
    {
        logger.LogWarning(
            "Security Audit: API key {ApiKeyId} revoked in project {ProjectId}",
            apiKeyId,
            projectId);
    }

    public void LogUserDeactivated(Guid userId)
    {
        logger.LogWarning(
            "Security Audit: User account {UserId} deactivated and active sessions revoked",
            userId);
    }
}
