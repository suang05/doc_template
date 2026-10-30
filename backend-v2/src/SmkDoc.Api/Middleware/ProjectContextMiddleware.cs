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

namespace SmkDoc.Api.Middleware;

/// <summary>
/// Centralized perimeter security gateway performing dual-channel authentication,
/// tenant isolation resolution, M2M scope enforcement, and IDOR defense across all incoming HTTP traffic.
/// Converges both X-API-Key (M2M) and Authorization Bearer (Web Studio) into <see cref="ICurrentProjectContextSetter"/>.
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
        IProjectRepository projectRepo)
    {
        string path = context.Request.Path.Value?.ToLowerInvariant() ?? string.Empty;
        string? clientIp = context.Connection.RemoteIpAddress?.ToString();

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

            UserId? parsedUserId = Guid.TryParse(userIdStr, out var userIdGuid) && userIdGuid != Guid.Empty
                ? new UserId(userIdGuid)
                : null;

            var systemRoleClaim = context.User.FindFirst("SystemRole")?.Value
                ?? context.User.FindFirst(ClaimTypes.Role)?.Value;
            bool isSuperAdmin = string.Equals(systemRoleClaim, RoleConstants.SuperAdmin, StringComparison.OrdinalIgnoreCase);
            var parsedSystemRole = isSuperAdmin ? SystemRole.SuperAdmin : SystemRole.Member;

            string actorIdentity = parsedUserId is { } uid ? $"User:{uid.Value:N}" : "portal";
            projectContextSetter.SetActor(
                userId: parsedUserId,
                systemRole: parsedSystemRole,
                callerIdentity: actorIdentity);

            // Unconditionally hydrate SuperAdmin + Admin claims even on global routes without a target ProjectId
            if (isSuperAdmin && context.User.Identity is ClaimsIdentity superIdentity)
            {
                EnsureRoleClaim(superIdentity, RoleConstants.SuperAdmin);
                EnsureRoleClaim(superIdentity, RoleConstants.Admin);
            }

            Guid? routeProjectIdGuid = ExtractRouteProjectId(context);
            Guid? headerProjectIdGuid = ExtractHeaderProjectId(context);

            // Strict Conflict Detection: Fail-fast if Route and Header contradict ([TRAP-SEC-004])
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

            // Fallback to ProjectId claim in user token if neither route nor header specified
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

                string roleName = RoleConstants.SuperAdmin;
                if (!isSuperAdmin)
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

                    if (context.User.Identity is ClaimsIdentity projectIdentity)
                    {
                        EnsureRoleClaim(projectIdentity, roleName);
                    }
                }

                projectContextSetter.SetProject(targetProjectId, actorIdentity);
            }

            await next(context);
            return;
        }

        // 3. Channel A: Machine-to-Machine (M2M) via X-API-Key Header
        if (context.Request.Headers.TryGetValue(HttpHeaderConstants.ApiKeyHeader, out var extractedApiKey) &&
            !string.IsNullOrWhiteSpace(extractedApiKey))
        {
            var rawKey = extractedApiKey.ToString();
            Guid? routeProjectIdGuid = ExtractRouteProjectId(context);
            Guid? headerProjectIdGuid = ExtractHeaderProjectId(context);

            // Bootstrap Master Key Support
            if (!string.IsNullOrEmpty(_masterApiKey) && rawKey == _masterApiKey)
            {
                projectContextSetter.SetActor(callerIdentity: "master");
                var masterProjId = routeProjectIdGuid ?? headerProjectIdGuid;
                if (masterProjId.HasValue)
                {
                    projectContextSetter.SetProject(new ProjectId(masterProjId.Value), "master");
                }

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
                logger.LogWarning("Invalid or inactive API Key attempt from {IpAddress} to {Path}", clientIp, path);
                await WriteProblemDetailsAsync(
                    context,
                    StatusCodes.Status401Unauthorized,
                    "Unauthorized",
                    "Invalid or inactive API Key.",
                    "https://tools.ietf.org/html/rfc9457");
                return;
            }

            // Reject cross-tenant project ID spoofing via X-Project-Id header or {projectId} route value
            Guid? spoofedProjectId = (headerProjectIdGuid.HasValue && headerProjectIdGuid.Value != key.ProjectId)
                ? headerProjectIdGuid
                : (routeProjectIdGuid.HasValue && routeProjectIdGuid.Value != key.ProjectId ? routeProjectIdGuid : null);

            if (spoofedProjectId.HasValue)
            {
                logger.LogWarning(
                    "Tenant spoofing attempt detected: API Key belongs to project {KeyProjectId}, but caller supplied {RequestedProjectId}",
                    key.ProjectId,
                    spoofedProjectId.Value);

                await WriteProblemDetailsAsync(
                    context,
                    StatusCodes.Status403Forbidden,
                    "Forbidden",
                    "Cross-tenant project ID spoofing is strictly prohibited for API key authentication.",
                    "https://tools.ietf.org/html/rfc9457");
                return;
            }

            var scope = ApiKeyScope.TryFromName(key.Scope, out var parsedScope) ? parsedScope : ApiKeyScope.ReadWrite;
            if (scope == ApiKeyScope.ReadOnly && IsMutatingNonStatelessRequest(context.Request, path))
            {
                logger.LogWarning(
                    "ReadOnly API Key {ApiKeyId} ({CallerApp}) blocked from mutating request {Method} {Path}",
                    key.Id,
                    key.CallerApp,
                    context.Request.Method,
                    path);

                await WriteProblemDetailsAsync(
                    context,
                    StatusCodes.Status403Forbidden,
                    "Forbidden",
                    "Read-only API keys cannot invoke state-mutating endpoints.",
                    "https://tools.ietf.org/html/rfc9457");
                return;
            }

            var projectId = new ProjectId(key.ProjectId);
            projectContextSetter.SetActor(apiKeyId: new ApiKeyId(key.Id), callerIdentity: key.CallerApp);
            projectContextSetter.SetProject(projectId, key.CallerApp);

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

    private static Guid? ExtractRouteProjectId(HttpContext context)
    {
        if (context.GetRouteValue("projectId") is object routeVal &&
            Guid.TryParse(routeVal.ToString(), out var routeProjId) &&
            routeProjId != Guid.Empty)
        {
            return routeProjId;
        }

        if (context.Request.RouteValues.TryGetValue("projectId", out var dictVal) &&
            dictVal != null &&
            Guid.TryParse(dictVal.ToString(), out var dictProjId) &&
            dictProjId != Guid.Empty)
        {
            return dictProjId;
        }

        var segments = context.Request.Path.Value?.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments != null)
        {
            for (int i = 0; i < segments.Length - 1; i++)
            {
                if (string.Equals(segments[i], "projects", StringComparison.OrdinalIgnoreCase) &&
                    Guid.TryParse(segments[i + 1], out var pathProjId) &&
                    pathProjId != Guid.Empty)
                {
                    return pathProjId;
                }
            }
        }

        return null;
    }

    private static Guid? ExtractHeaderProjectId(HttpContext context)
    {
        if (context.Request.Headers.TryGetValue(HttpHeaderConstants.ProjectIdHeader, out var reqProjectIdStr) &&
            Guid.TryParse(reqProjectIdStr, out var headerProjId) &&
            headerProjId != Guid.Empty)
        {
            return headerProjId;
        }

        return null;
    }

    private static void EnsureRoleClaim(ClaimsIdentity identity, string role)
    {
        if (!identity.HasClaim(ClaimTypes.Role, role))
        {
            identity.AddClaim(new Claim(ClaimTypes.Role, role));
        }
    }

    private static bool IsMutatingNonStatelessRequest(HttpRequest request, string normalizedLowerPath)
    {
        if (HttpMethods.IsGet(request.Method) ||
            HttpMethods.IsHead(request.Method) ||
            HttpMethods.IsOptions(request.Method))
        {
            return false;
        }

        if (HttpMethods.IsPost(request.Method))
        {
            bool isStatelessPreviewOrValidate =
                normalizedLowerPath.StartsWith("/api/v1/documents/preview", StringComparison.Ordinal)
                || normalizedLowerPath.StartsWith("/api/documents/preview", StringComparison.Ordinal)
                || normalizedLowerPath.StartsWith("/api/v1/rendering/preview", StringComparison.Ordinal)
                || normalizedLowerPath.StartsWith("/api/rendering/preview", StringComparison.Ordinal)
                || normalizedLowerPath.EndsWith("/validate", StringComparison.Ordinal)
                || normalizedLowerPath.EndsWith("/validate-payload", StringComparison.Ordinal);

            return !isStatelessPreviewOrValidate;
        }

        return true;
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
            || path.StartsWith("/api/schemas/validate", StringComparison.OrdinalIgnoreCase);
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
