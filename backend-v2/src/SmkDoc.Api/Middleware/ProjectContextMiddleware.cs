using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using SmkDoc.Api.Common.Responses;
using SmkDoc.Api.Constants;
using SmkDoc.Api.OpenApi;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.IdentityAccess.Security.DTOs;
using SmkDoc.Application.Modules.IdentityAccess.Security.Queries.ValidateApiKey;
using SmkDoc.Domain.Enums;
using SmkDoc.Domain.Exceptions;
using SmkDoc.Domain.ValueObjects.Ids;

namespace SmkDoc.Api.Middleware;

/// <summary>
/// Centralized perimeter security gateway performing dual-channel authentication,
/// tenant isolation resolution, M2M scope enforcement, and IDOR defense across all incoming HTTP traffic.
/// Converges both X-API-Key (M2M) and Authorization Bearer (Web Studio) into <see cref="ICurrentProjectContextSetter"/>.
/// </summary>
public sealed partial class ProjectContextMiddleware(
    RequestDelegate next,
    ILogger<ProjectContextMiddleware> logger,
    IConfiguration configuration)
{
    private readonly string? _masterApiKey = configuration["MASTER_API_KEY"] ?? configuration["Security:ApiKey"];
    private readonly string? _problemTypeBaseUri = configuration["Problems:BaseUri"];

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Tenant conflict detected: Route specifies project {RouteProjectId}, but Project-Id header specifies {HeaderProjectId}")]
    private static partial void LogTenantConflict(ILogger logger, Guid routeProjectId, Guid headerProjectId);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Perimeter defense: Project context required for data plane route {Path}")]
    private static partial void LogPerimeterProjectRequired(ILogger logger, string path);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Tenant resolution failed: Project {ProjectId} not found")]
    private static partial void LogTenantNotFound(ILogger logger, ProjectId projectId);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Tenant inactive defense: User {UserId} denied access to deactivated project {ProjectId}")]
    private static partial void LogTenantInactiveForUser(ILogger logger, Guid userId, ProjectId projectId);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Zero Standing Access defense: User {UserId} (SystemRole: {SystemRole}) denied access to project {ProjectId} - no project membership")]
    private static partial void LogZeroStandingAccessDenied(ILogger logger, Guid userId, SystemRole systemRole, ProjectId projectId);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Tenant inactive defense: API Key denied access because project {ProjectId} is deactivated")]
    private static partial void LogTenantInactiveForApiKey(ILogger logger, Guid projectId);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Invalid or inactive API Key attempt from {IpAddress} to {Path}")]
    private static partial void LogInvalidApiKeyAttempt(ILogger logger, string? ipAddress, string path);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Tenant context mismatch detected: API Key belongs to project {KeyProjectId}, but caller supplied {RequestedProjectId}")]
    private static partial void LogApiKeyTenantMismatch(ILogger logger, Guid keyProjectId, Guid requestedProjectId);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "ReadOnly API Key {ApiKeyId} ({CallerApp}) blocked from mutating request {Method} {Path}")]
    private static partial void LogReadOnlyApiKeyBlocked(ILogger logger, Guid apiKeyId, string callerApp, string method, string path);

    public async Task InvokeAsync(
        HttpContext context,
        ICurrentProjectContextSetter projectContextSetter,
        IUseCase<ValidateApiKeyQuery, ValidatedApiKeyResultDto?> validateApiKeyUseCase,
        ITenantResolutionPort tenantResolutionPort)
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

            // Zero Standing Access: Hydrate SuperAdmin claim only (do NOT auto-escalate SuperAdmin to tenant Admin)
            if (isSuperAdmin && context.User.Identity is ClaimsIdentity superIdentity)
            {
                EnsureRoleClaim(superIdentity, RoleConstants.SuperAdmin);
            }

            bool isManagementRoute = path.StartsWith("/api/v1/management/", StringComparison.OrdinalIgnoreCase);

            Guid? routeProjectIdGuid = ExtractRouteProjectId(context);
            Guid? headerProjectIdGuid = ExtractHeaderProjectId(context);

            // Strict Conflict Detection: Fail-fast if Route and Header contradict ([TRAP-SEC-004])
            if (routeProjectIdGuid.HasValue && headerProjectIdGuid.HasValue && routeProjectIdGuid.Value != headerProjectIdGuid.Value)
            {
                LogTenantConflict(logger, routeProjectIdGuid.Value, headerProjectIdGuid.Value);

                await WriteProblemDetailsAsync(
                    context,
                    StatusCodes.Status400BadRequest,
                    "Bad Request",
                    $"Tenant conflict detected: Route specifies project '{routeProjectIdGuid.Value}', but Project-Id header specifies '{headerProjectIdGuid.Value}'.",
                    ErrorCodes.ProjectContextMismatch);
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

            // Fail-fast perimeter guard: Data plane operations strictly require a resolved tenant context
            if (!isManagementRoute && !targetProjectIdGuid.HasValue)
            {
                LogPerimeterProjectRequired(logger, path);
                await WriteProblemDetailsAsync(
                    context,
                    StatusCodes.Status400BadRequest,
                    "Bad Request",
                    "Project context is required. Provide a Project-Id header or valid project claim.",
                    ErrorCodes.ProjectContextRequired);
                return;
            }

            // If a specific project was requested, enforce active tenant validation & IDOR defense
            if (targetProjectIdGuid.HasValue)
            {
                var targetProjectId = new ProjectId(targetProjectIdGuid.Value);

                var project = await tenantResolutionPort.FindProjectAsync(targetProjectId, context.RequestAborted);
                if (project == null)
                {
                    LogTenantNotFound(logger, targetProjectId);
                    await WriteProblemDetailsAsync(
                        context,
                        StatusCodes.Status404NotFound,
                        "Not Found",
                        $"Project '{targetProjectId}' not found.",
                        ErrorCodes.NotFound);
                    return;
                }

                if (!project.IsActive)
                {
                    LogTenantInactiveForUser(logger, userIdGuid, targetProjectId);

                    await WriteProblemDetailsAsync(
                        context,
                        StatusCodes.Status403Forbidden,
                        "Forbidden",
                        $"Project '{targetProjectId}' is deactivated.",
                        ErrorCodes.ProjectDeactivated);
                    return;
                }

                if (isManagementRoute && isSuperAdmin)
                {
                    // Control Plane: SuperAdmin manages project container without requiring tenant membership
                    projectContextSetter.SetProject(targetProjectId, actorIdentity);
                }
                else
                {
                    // Data Plane (or non-SuperAdmin): Strict tenant membership verification required (Zero Standing Access)
                    var roleMapping = await tenantResolutionPort.FindMembershipAsync(
                        targetProjectId,
                        new UserId(userIdGuid),
                        context.RequestAborted);

                    if (roleMapping == null)
                    {
                        LogZeroStandingAccessDenied(logger, userIdGuid, parsedSystemRole, targetProjectId);

                        await WriteProblemDetailsAsync(
                            context,
                            StatusCodes.Status403Forbidden,
                            "Forbidden",
                            $"User does not have permission to access project '{targetProjectId}'.",
                            ErrorCodes.CrossTenantAccessDenied);
                        return;
                    }

                    var roleName = roleMapping.RoleName;

                    if (context.User.Identity is ClaimsIdentity projectIdentity)
                    {
                        EnsureRoleClaim(projectIdentity, roleName);
                    }

                    projectContextSetter.SetProject(targetProjectId, actorIdentity);
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
            Guid? routeProjectIdGuid = ExtractRouteProjectId(context);
            Guid? headerProjectIdGuid = ExtractHeaderProjectId(context);

            // Bootstrap Master Key Support (Constant-time comparison)
            if (!string.IsNullOrEmpty(_masterApiKey) &&
                CryptographicOperations.FixedTimeEquals(
                    Encoding.UTF8.GetBytes(rawKey),
                    Encoding.UTF8.GetBytes(_masterApiKey)))
            {
                projectContextSetter.SetActor(callerIdentity: "master");
                var masterProjId = routeProjectIdGuid ?? headerProjectIdGuid;
                if (masterProjId.HasValue)
                {
                    projectContextSetter.SetProject(new ProjectId(masterProjId.Value), "master");
                }

                var masterIdentity = new ClaimsIdentity("ApiKeyScheme");
                masterIdentity.AddClaim(new Claim(ClaimTypes.NameIdentifier, "master"));
                masterIdentity.AddClaim(new Claim(ClaimTypes.Role, RoleConstants.SuperAdmin));
                masterIdentity.AddClaim(new Claim(ClaimTypes.Role, RoleConstants.Admin));
                masterIdentity.AddClaim(new Claim("SystemRole", RoleConstants.SuperAdmin));
                masterIdentity.AddClaim(new Claim("ApiKeyScope", ApiKeyScope.ReadWrite.Name));
                masterIdentity.AddClaim(new Claim("Scope", ApiKeyScope.ReadWrite.Name));
                masterIdentity.AddClaim(new Claim("CallerApp", "master"));
                context.User = new ClaimsPrincipal(masterIdentity);

                await next(context);
                return;
            }

            ValidatedApiKeyResultDto? key;
            try
            {
                var query = new ValidateApiKeyQuery(rawKey);
                key = await validateApiKeyUseCase.ExecuteAsync(query, context.RequestAborted);
            }
            catch (ProjectDeactivatedException ex)
            {
                LogTenantInactiveForApiKey(logger, ex.ProjectId);

                await WriteProblemDetailsAsync(
                    context,
                    StatusCodes.Status403Forbidden,
                    "Forbidden",
                    ex.Message,
                    ErrorCodes.ProjectDeactivated);
                return;
            }

            if (key == null)
            {
                LogInvalidApiKeyAttempt(logger, clientIp, path);
                await WriteProblemDetailsAsync(
                    context,
                    StatusCodes.Status401Unauthorized,
                    "Unauthorized",
                    "Invalid or inactive API Key.",
                    ErrorCodes.Unauthorized);
                return;
            }

            // Reject cross-tenant project ID spoofing via Project-Id header or {projectId} route value
            Guid? spoofedProjectId = (headerProjectIdGuid.HasValue && headerProjectIdGuid.Value != key.ProjectId)
                ? headerProjectIdGuid
                : (routeProjectIdGuid.HasValue && routeProjectIdGuid.Value != key.ProjectId ? routeProjectIdGuid : null);

            if (spoofedProjectId.HasValue)
            {
                LogApiKeyTenantMismatch(logger, key.ProjectId, spoofedProjectId.Value);

                await WriteProblemDetailsAsync(
                    context,
                    StatusCodes.Status400BadRequest,
                    "Bad Request",
                    "Project-Id does not match the project scoped to the API key.",
                    ErrorCodes.ProjectContextMismatch);
                return;
            }

            var scope = ApiKeyScope.TryFromName(key.Scope, out var parsedScope) ? parsedScope : ApiKeyScope.ReadWrite;
            if (scope == ApiKeyScope.ReadOnly && IsMutatingNonStatelessRequest(context.Request, path))
            {
                if (logger.IsEnabled(LogLevel.Warning))
                {
                    LogReadOnlyApiKeyBlocked(logger, key.Id, key.CallerApp, context.Request.Method, path);
                }

                await WriteProblemDetailsAsync(
                    context,
                    StatusCodes.Status403Forbidden,
                    "Forbidden",
                    "Read-only API keys cannot invoke state-mutating endpoints.",
                    ErrorCodes.InsufficientKeyScope);
                return;
            }

            var projectId = new ProjectId(key.ProjectId);
            projectContextSetter.SetActor(apiKeyId: new ApiKeyId(key.Id), callerIdentity: key.CallerApp);
            projectContextSetter.SetProject(projectId, key.CallerApp);

            // Hydrate ClaimsPrincipal onto HttpContext.User for ASP.NET Core [Authorize] authorization
            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, key.Id.ToString()),
                new(ClaimTypes.Role, "M2MClient"),
                new("Scope", key.Scope),
                new("ApiKeyScope", key.Scope),
                new("ProjectId", key.ProjectId.ToString()),
                new("CallerApp", key.CallerApp)
            };

            if (string.Equals(key.Scope, ApiKeyScope.ReadWrite.Name, StringComparison.OrdinalIgnoreCase))
            {
                claims.Add(new Claim(ClaimTypes.Role, RoleConstants.Developer));
            }
            claims.Add(new Claim(ClaimTypes.Role, RoleConstants.Member));

            var identity = new ClaimsIdentity(claims, "ApiKeyScheme");
            context.User = new ClaimsPrincipal(identity);

            await next(context);
            return;
        }

        // 4. Secured Path Fallback: Reject unauthenticated request
        await WriteProblemDetailsAsync(
            context,
            StatusCodes.Status401Unauthorized,
            "Unauthorized",
            "Authentication credentials missing or invalid. Provide an X-API-Key header or Bearer token.",
            ErrorCodes.Unauthorized);
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
                || normalizedLowerPath.StartsWith("/api/v1/rendering/preview", StringComparison.Ordinal)
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
            || OpenApiNames.PublicRoutes.Any(route => path.StartsWith(route, StringComparison.OrdinalIgnoreCase));
    }

    private async Task WriteProblemDetailsAsync(
        HttpContext context,
        int statusCode,
        string title,
        string detail,
        string? customErrorCode = null)
    {
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/problem+json";

        if (statusCode == StatusCodes.Status401Unauthorized &&
            !context.Response.Headers.ContainsKey("WWW-Authenticate"))
        {
            context.Response.Headers.WWWAuthenticate = "Bearer";
        }

        var errorCode = customErrorCode ?? ProblemDetailsBuilder.ResolveDefaultErrorCode(statusCode);
        var problemDetails = ProblemDetailsBuilder.Create(
            status: statusCode,
            title: title,
            detail: detail,
            errorCode: errorCode,
            instance: context.Request.Path.Value,
            traceId: ProblemDetailsBuilder.ResolveTraceId(context),
            typeBaseUri: _problemTypeBaseUri);

        await context.Response.WriteAsJsonAsync(
            problemDetails,
            options: (System.Text.Json.JsonSerializerOptions?)null,
            contentType: "application/problem+json");
    }
}
