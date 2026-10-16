using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.IdentityAccess.Security.DTOs;
using SmkDoc.Application.Modules.IdentityAccess.Security.Queries.ValidateApiKey;
using SmkDoc.Domain.Enums;
using SmkDoc.Domain.Interfaces;
using SmkDoc.Domain.ValueObjects.Ids;
using SmkDoc.Infrastructure.Contexts;

namespace SmkDoc.Api.Middleware;

/// <summary>
/// Centralized perimeter security gateway performing dual-channel authentication,
/// tenant isolation resolution, and IDOR defense across all incoming HTTP traffic.
/// </summary>
public class ProjectContextMiddleware(
    RequestDelegate next,
    ILogger<ProjectContextMiddleware> logger,
    IConfiguration configuration)
{
    private readonly string? _masterApiKey = configuration["MASTER_API_KEY"] ?? configuration["Security:ApiKey"];

    public async Task InvokeAsync(
        HttpContext context,
        ICurrentProjectContextSetter projectContextSetter,
        ValidateApiKeyUseCase validateApiKeyUseCase,
        IUserProjectRoleRepository userProjectRoleRepo,
        ExecutionContextImpl executionContext)
    {
        string path = context.Request.Path.Value?.ToLowerInvariant() ?? string.Empty;

        // Capture client telemetry on execution context
        executionContext.ClientIp = context.Connection.RemoteIpAddress?.ToString();
        executionContext.UserAgent = context.Request.Headers.UserAgent.ToString();

        // 1. Whitelist Bypass for Public Endpoints
        if (IsWhitelistedPath(path))
        {
            await next(context);
            return;
        }

        // 2. Channel A: Machine-to-Machine (M2M) via X-API-Key Header
        if (context.Request.Headers.TryGetValue("X-API-Key", out var extractedApiKey) &&
            !string.IsNullOrWhiteSpace(extractedApiKey))
        {
            var rawKey = extractedApiKey.ToString();

            // Bootstrap Master Key Support
            if (!string.IsNullOrEmpty(_masterApiKey) && rawKey == _masterApiKey)
            {
                if (context.Request.Headers.TryGetValue("X-Project-Id", out var masterProjectHeader) &&
                    Guid.TryParse(masterProjectHeader, out var masterProjId) &&
                    masterProjId != Guid.Empty)
                {
                    projectContextSetter.SetProject(new ProjectId(masterProjId), "MasterKey");
                    executionContext.ProjectId = masterProjId;
                }

                executionContext.CallerApp = "master";
                executionContext.Scope = ApiKeyScope.ReadWrite;
                await next(context);
                return;
            }

            var key = await validateApiKeyUseCase.ExecuteAsync(new ValidateApiKeyQuery(rawKey), context.RequestAborted);
            if (key == null)
            {
                logger.LogWarning("Invalid or inactive API Key attempt from {IpAddress} to {Path}", executionContext.ClientIp, path);
                await WriteProblemDetailsAsync(
                    context,
                    StatusCodes.Status401Unauthorized,
                    "Unauthorized",
                    "Invalid or inactive API Key.",
                    "https://tools.ietf.org/html/rfc9457");
                return;
            }

            var projectId = new ProjectId(key.ProjectId);
            projectContextSetter.SetProject(projectId, $"ApiKey:{key.CallerApp}:{key.Id}");

            executionContext.ApiKeyId = key.Id;
            executionContext.CallerApp = key.CallerApp;
            executionContext.ProjectId = key.ProjectId;
            executionContext.Scope = ApiKeyScope.TryFromName(key.Scope, out var scope) ? scope : ApiKeyScope.ReadWrite;

            await next(context);
            return;
        }

        // 3. Channel B: Interactive Portal User via Bearer JWT
        if (context.User.Identity?.IsAuthenticated == true)
        {
            var userIdStr = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                ?? context.User.FindFirst("sub")?.Value;

            if (Guid.TryParse(userIdStr, out var userIdGuid) && userIdGuid != Guid.Empty)
            {
                executionContext.UserId = userIdGuid;
            }

            executionContext.CallerApp = "portal";
            executionContext.Scope = ApiKeyScope.ReadWrite;

            // Resolve target project: Prefer X-Project-Id header, then ProjectId claim
            Guid? targetProjectIdGuid = null;
            if (context.Request.Headers.TryGetValue("X-Project-Id", out var reqProjectIdStr) &&
                Guid.TryParse(reqProjectIdStr, out var headerProjId) &&
                headerProjId != Guid.Empty)
            {
                targetProjectIdGuid = headerProjId;
            }
            else
            {
                var claimProjectIdStr = context.User.FindFirst("ProjectId")?.Value;
                if (Guid.TryParse(claimProjectIdStr, out var claimProjId) && claimProjId != Guid.Empty)
                {
                    targetProjectIdGuid = claimProjId;
                }
            }

            // If a specific project was requested, enforce IDOR defense
            if (targetProjectIdGuid.HasValue)
            {
                var targetProjectId = new ProjectId(targetProjectIdGuid.Value);

                var systemRoleClaim = context.User.FindFirst("SystemRole")?.Value
                    ?? context.User.FindFirst(ClaimTypes.Role)?.Value;
                bool isSuperAdmin = string.Equals(systemRoleClaim, "SuperAdmin", StringComparison.OrdinalIgnoreCase);

                string roleName = "Member";
                if (isSuperAdmin)
                {
                    roleName = "SuperAdmin";
                }
                else
                {
                    var roleMapping = await userProjectRoleRepo.GetAsync(
                        targetProjectId.Value,
                        userIdGuid,
                        context.RequestAborted);

                    if (roleMapping == null)
                    {
                        logger.LogWarning(
                            "IDOR defense: User {UserId} denied access to project {ProjectId}",
                            userIdGuid,
                            targetProjectId);

                        await WriteProblemDetailsAsync(
                            context,
                            StatusCodes.Status403Forbidden,
                            "Forbidden",
                            $"User does not have permission to access project '{targetProjectId}'.",
                            "https://tools.ietf.org/html/rfc9457");
                        return;
                    }

                    roleName = roleMapping.Role.Name;
                }

                projectContextSetter.SetProject(targetProjectId, $"User:{userIdGuid}:{roleName}");
                executionContext.ProjectId = targetProjectId.Value;
            }

            await next(context);
            return;
        }

        // 4. Secured Path Fallback: Reject unauthenticated request
        await WriteProblemDetailsAsync(
            context,
            StatusCodes.Status401Unauthorized,
            "Unauthorized",
            "Authentication credentials missing or invalid. Provide an X-API-Key header or Bearer token.",
            "https://tools.ietf.org/html/rfc9457");
    }

    private static bool IsWhitelistedPath(string path)
    {
        if (string.IsNullOrEmpty(path) || path == "/")
        {
            return true;
        }

        return path.Equals("/health", StringComparison.OrdinalIgnoreCase)
            || path.Equals("/healthz", StringComparison.OrdinalIgnoreCase)
            || path.Equals("/ready", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/scalar", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/openapi", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/swagger", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/docs", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/api/v1/auth/login", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/api/v1/auth/refresh", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/api/v1/schemas/validate", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/api/schemas/validate", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/api/documents/preview", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/api/v1/documents/preview", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/api/v1/rendering/preview", StringComparison.OrdinalIgnoreCase);
    }

    private static async Task WriteProblemDetailsAsync(
        HttpContext context,
        int statusCode,
        string title,
        string detail,
        string type)
    {
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/problem+json";

        var problemDetails = new
        {
            type,
            title,
            status = statusCode,
            detail,
            instance = context.Request.Path.Value
        };

        await context.Response.WriteAsJsonAsync(
            problemDetails,
            options: (System.Text.Json.JsonSerializerOptions?)null,
            contentType: "application/problem+json");
    }
}
