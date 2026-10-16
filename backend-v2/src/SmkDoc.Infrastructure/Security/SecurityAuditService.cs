using Microsoft.Extensions.Logging;
using SmkDoc.Application.Common.Interfaces;

namespace SmkDoc.Infrastructure.Security;

/// <summary>
/// Infrastructure adapter implementing security audit logging via Microsoft.Extensions.Logging (Serilog / SIEM).
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
}
