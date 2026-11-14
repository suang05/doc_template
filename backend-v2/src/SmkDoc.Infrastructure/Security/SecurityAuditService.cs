using Microsoft.Extensions.Logging;
using SmkDoc.Application.Common.Interfaces;

namespace SmkDoc.Infrastructure.Security;

/// <summary>
/// Infrastructure adapter implementing security and domain lifecycle audit logging via Microsoft.Extensions.Logging (Serilog / SIEM).
/// </summary>
public sealed partial class SecurityAuditService(ILogger<SecurityAuditService> logger) : ISecurityAuditService
{
    public void LogLoginSuccess(Guid userId, string email, Guid? activeProjectId) =>
        WriteLoginSuccess(logger, userId, email, activeProjectId);

    public void LogLoginFailure(string email, string reason) =>
        WriteLoginFailure(logger, email, reason);

    public void LogTokenRotated(Guid userId) =>
        WriteTokenRotated(logger, userId);

    public void LogTokenReuseDetected(Guid userId) =>
        WriteTokenReuseDetected(logger, userId);

    public void LogTemplatePublished(Guid projectId, Guid templateId, Guid versionId, int versionNumber) =>
        WriteTemplatePublished(logger, templateId, versionNumber, versionId, projectId);

    public void LogDocumentVersionCreated(Guid projectId, Guid documentId, int versionNumber, Guid? generationLogId) =>
        WriteDocumentVersionCreated(logger, documentId, versionNumber, generationLogId, projectId);

    public void LogApiKeyRevoked(Guid projectId, Guid apiKeyId) =>
        WriteApiKeyRevoked(logger, apiKeyId, projectId);

    public void LogUserDeactivated(Guid userId) =>
        WriteUserDeactivated(logger, userId);

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Security Audit: User {UserId} ({Email}) authenticated successfully with active project {ProjectId}")]
    private static partial void WriteLoginSuccess(ILogger logger, Guid userId, string email, Guid? projectId);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Security Audit: Authentication failed for {Email}. Reason: {Reason}")]
    private static partial void WriteLoginFailure(ILogger logger, string email, string reason);

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Security Audit: Refresh token rotated successfully for user {UserId}")]
    private static partial void WriteTokenRotated(ILogger logger, Guid userId);

    [LoggerMessage(
        Level = LogLevel.Critical,
        Message = "Security Alert: Refresh token reuse detected for user {UserId}. Nuclear session revocation executed.")]
    private static partial void WriteTokenReuseDetected(ILogger logger, Guid userId);

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Domain Audit: Published template {TemplateId} version {VersionNumber} ({VersionId}) in project {ProjectId}")]
    private static partial void WriteTemplatePublished(ILogger logger, Guid templateId, int versionNumber, Guid versionId, Guid projectId);

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Domain Audit: Recorded document {DocumentId} version {VersionNumber} (GenerationLogId={GenerationLogId}) in project {ProjectId}")]
    private static partial void WriteDocumentVersionCreated(ILogger logger, Guid documentId, int versionNumber, Guid? generationLogId, Guid projectId);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Security Audit: API key {ApiKeyId} revoked in project {ProjectId}")]
    private static partial void WriteApiKeyRevoked(ILogger logger, Guid apiKeyId, Guid projectId);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Security Audit: User account {UserId} deactivated and active sessions revoked")]
    private static partial void WriteUserDeactivated(ILogger logger, Guid userId);
}
