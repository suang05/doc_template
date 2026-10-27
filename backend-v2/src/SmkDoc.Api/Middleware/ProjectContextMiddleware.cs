using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SmkDoc.Api.Constants;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.IdentityAccess.Security.DTOs;
using SmkDoc.Application.Modules.IdentityAccess.Security.Queries.ValidateApiKey;
using SmkDoc.Domain.Enums;
using SmkDoc.Domain.Exceptions;
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
        IProjectRepository projectRepo,
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

        // 2. Channel B: Interactive Portal User via Bearer JWT (Strict precedence over X-API-Key)
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

            // 1. Resolve target project from Route Values (e.g. /api/v1/management/projects/{projectId}/...)
            Guid? routeProjectIdGuid = null;
            if (context.GetRouteValue("projectId") is object routeVal &&
                Guid.TryParse(routeVal.ToString(), out var routeProjId) &&
                routeProjId != Guid.Empty)
            {
                routeProjectIdGuid = routeProjId;
            }
            else if (context.Request.RouteValues.TryGetValue("projectId", out var dictVal) &&
                dictVal != null &&
                Guid.TryParse(dictVal.ToString(), out var dictProjId) &&
                dictProjId != Guid.Empty)
            {
                routeProjectIdGuid = dictProjId;
            }
            else
            {
                // Fallback: Path segments extraction for /projects/{guid}
                var segments = context.Request.Path.Value?.Split('/', StringSplitOptions.RemoveEmptyEntries);
                if (segments != null)
                {
                    for (int i = 0; i < segments.Length - 1; i++)
                    {
                        if (string.Equals(segments[i], "projects", StringComparison.OrdinalIgnoreCase) &&
                            Guid.TryParse(segments[i + 1], out var pathProjId) &&
                            pathProjId != Guid.Empty)
                        {
                            routeProjectIdGuid = pathProjId;
                            break;
                        }
                    }
                }
            }

            // 2. Resolve target project from Header X-Project-Id
            Guid? headerProjectIdGuid = null;
            if (context.Request.Headers.TryGetValue(HttpHeaderConstants.ProjectIdHeader, out var reqProjectIdStr) &&
                Guid.TryParse(reqProjectIdStr, out var headerProjId) &&
                headerProjId != Guid.Empty)
            {
                headerProjectIdGuid = headerProjId;
            }

            // 3. Strict Conflict Detection: Fail-fast if Route and Header contradict ([TRAP-SEC-004])
            if (routeProjectIdGuid.HasValue && headerProjectIdGuid.HasValue && routeProjectIdGuid.Value != headerProjectIdGuid.Value)
            {
                logger.LogWarning(
                    "Tenant conflict detected: Route specifies project {RouteProjectId}, but X-Project-Id header specifies {HeaderProjectId}",
                    routeProjectIdGuid.Value,
                    headerProjectIdGuid.Value);

                await WriteProblemDetailsAsync(
                    context,
                    StatusCodes.Status400BadRequest,
                    "Bad Request",
                    $"Tenant conflict detected: Route specifies project '{routeProjectIdGuid.Value}', but X-Project-Id header specifies '{headerProjectIdGuid.Value}'.",
                    "https://tools.ietf.org/html/rfc9457");
                return;
            }

            Guid? targetProjectIdGuid = routeProjectIdGuid ?? headerProjectIdGuid;

            // 4. Fallback to ProjectId claim in user token if neither route nor header specified
            if (!targetProjectIdGuid.HasValue)
            {
                var claimProjectIdStr = context.User.FindFirst("ProjectId")?.Value;
                if (Guid.TryParse(claimProjectIdStr, out var claimProjId) && claimProjId != Guid.Empty)
                {
                    targetProjectIdGuid = claimProjId;
                }
            }

            // If a specific project was requested, enforce active tenant validation & IDOR defense
            if (targetProjectIdGuid.HasValue)
            {
                var targetProjectId = new ProjectId(targetProjectIdGuid.Value);

                var project = await projectRepo.GetByIdAsync(targetProjectId, context.RequestAborted);
                if (project == null)
                {
                    logger.LogWarning("Tenant resolution failed: Project {ProjectId} not found", targetProjectId);
                    await WriteProblemDetailsAsync(
                        context,
                        StatusCodes.Status404NotFound,
                        "Not Found",
                        $"Project '{targetProjectId}' not found.",
                        "https://tools.ietf.org/html/rfc9457");
                    return;
                }

                if (!project.IsActive)
                {
                    logger.LogWarning(
                        "Tenant inactive defense: User {UserId} denied access to deactivated project {ProjectId}",
                        userIdGuid,
                        targetProjectId);

                    await WriteProblemDetailsAsync(
                        context,
                        StatusCodes.Status403Forbidden,
                        "Forbidden",
                        $"Project '{targetProjectId}' is deactivated.",
                        "https://tools.ietf.org/html/rfc9457");
                    return;
                }

                var systemRoleClaim = context.User.FindFirst("SystemRole")?.Value
                    ?? context.User.FindFirst(ClaimTypes.Role)?.Value;
                bool isSuperAdmin = string.Equals(systemRoleClaim, RoleConstants.SuperAdmin, StringComparison.OrdinalIgnoreCase);

                string roleName = RoleConstants.Member;
                if (isSuperAdmin)
                {
                    roleName = RoleConstants.SuperAdmin;
                }
                else
                {
                    var roleMapping = await userProjectRoleRepo.GetAsync(
                        targetProjectId,
                        new UserId(userIdGuid),
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

                // Dynamic ClaimTypes.Role Hydration with SuperAdmin privilege inheritance
                if (context.User.Identity is ClaimsIdentity identity)
                {
                    if (isSuperAdmin)
                    {
                        identity.AddClaim(new Claim(ClaimTypes.Role, RoleConstants.SuperAdmin));
                        identity.AddClaim(new Claim(ClaimTypes.Role, RoleConstants.Admin));
                    }
                    else
                    {
                        identity.AddClaim(new Claim(ClaimTypes.Role, roleName));
                    }
                }
            }

            await next(context);
            return;
        }

        // 3. Channel A: Machine-to-Machine (M2M) via X-API-Key Header
        if (context.Request.Headers.TryGetValue(HttpHeaderConstants.ApiKeyHeader, out var extractedApiKey) &&
            !string.IsNullOrWhiteSpace(extractedApiKey))
        {
            var rawKey = extractedApiKey.ToString();

            // Bootstrap Master Key Support
            if (!string.IsNullOrEmpty(_masterApiKey) && rawKey == _masterApiKey)
            {
                if (context.Request.Headers.TryGetValue(HttpHeaderConstants.ProjectIdHeader, out var masterProjectHeader) &&
                    Guid.TryParse(masterProjectHeader, out var masterProjId) &&
                    masterProjId != Guid.Empty)
                {
                    projectContextSetter.SetProject(new ProjectId(masterProjId), "MasterKey");
                }

                executionContext.CallerApp = "master";
                executionContext.Scope = ApiKeyScope.ReadWrite;
                await next(context);
                return;
            }

            ValidatedApiKeyDto? key;
            try
            {
                key = await validateApiKeyUseCase.ExecuteAsync(new ValidateApiKeyQuery(rawKey), context.RequestAborted);
            }
            catch (ProjectDeactivatedException ex)
            {
                logger.LogWarning(
                    "Tenant inactive defense: API Key denied access because project {ProjectId} is deactivated",
                    ex.ProjectId);

                await WriteProblemDetailsAsync(
                    context,
                    StatusCodes.Status403Forbidden,
                    "Forbidden",
                    ex.Message,
                    "https://tools.ietf.org/html/rfc9457");
                return;
            }

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

            // Reject cross-tenant project ID spoofing for normal API keys
            if (context.Request.Headers.TryGetValue(HttpHeaderConstants.ProjectIdHeader, out var reqProjHeader) &&
                Guid.TryParse(reqProjHeader, out var requestedProjectId) &&
                requestedProjectId != Guid.Empty &&
                requestedProjectId != key.ProjectId)
            {
                logger.LogWarning(
                    "Tenant spoofing attempt detected: API Key belongs to project {KeyProjectId}, but caller supplied X-Project-Id {RequestedProjectId}",
                    key.ProjectId,
                    requestedProjectId);

                await WriteProblemDetailsAsync(
                    context,
                    StatusCodes.Status403Forbidden,
                    "Forbidden",
                    "Cross-tenant project ID spoofing is strictly prohibited for API key authentication.",
                    "https://tools.ietf.org/html/rfc9457");
                return;
            }

            var projectId = new ProjectId(key.ProjectId);
            projectContextSetter.SetProject(projectId, $"ApiKey:{key.CallerApp}:{key.Id}");

            executionContext.ApiKeyId = key.Id;
            executionContext.CallerApp = key.CallerApp;
            executionContext.Scope = ApiKeyScope.TryFromName(key.Scope, out var scope) ? scope : ApiKeyScope.ReadWrite;

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
