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
/// Centralized perimeter security gateway enforcing dual-channel authentication,
/// Control Plane vs Data Plane separation, Zero Standing Access (ZSA), and strict tenant scoping.
/// Converges X-API-Key (M2M) and Authorization Bearer (Web Studio) into <see cref="ICurrentProjectContextSetter"/>.
/// </summary>
public sealed partial class ProjectContextMiddleware(
    RequestDelegate next,
    ILogger<ProjectContextMiddleware> logger,
    IConfiguration configuration)
{
    private readonly string? _masterApiKey = configuration["MASTER_API_KEY"] ?? configuration["Security:ApiKey"];
    private readonly Uri? _problemTypeBaseUri = ProblemDetailsBuilder.TryParseBaseUri(configuration["Problems:BaseUri"]);

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
        Message = "Zero Standing Access defense: User {UserId} (SystemRole: {SystemRole}) denied access to project {ProjectId} - no active membership or break-glass grant")]
    private static partial void LogZeroStandingAccessDenied(ILogger logger, Guid userId, SystemRole systemRole, ProjectId projectId);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "User revocation defense: Inactive or revoked user {UserId} denied access to {Path}")]
    private static partial void LogInactiveUserDenied(ILogger logger, Guid userId, string path);

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
        ITenantResolutionResolver tenantResolutionResolver,
        ISecurityAuditService securityAuditService,
        TimeProvider timeProvider)
    {
        string path = context.Request.Path.Value ?? string.Empty;

        if (IsWhitelistedPath(path))
        {
            if (path.StartsWith("/api/v1/auth/", StringComparison.OrdinalIgnoreCase))
            {
                projectContextSetter.SetControlPlane("auth");
            }

            await next(context);
            return;
        }

        if (context.User.Identity?.IsAuthenticated == true)
        {
            await HandlePortalUserAsync(
                context,
                projectContextSetter,
                tenantResolutionResolver,
                securityAuditService,
                timeProvider,
                path);
            return;
        }

        if (context.Request.Headers.TryGetValue(HttpHeaderConstants.ApiKeyHeader, out var extractedApiKey) &&
            !string.IsNullOrWhiteSpace(extractedApiKey))
        {
            await HandleApiKeyAuthAsync(context, projectContextSetter, validateApiKeyUseCase, extractedApiKey.ToString(), path);
            return;
        }

        await WriteProblemDetailsAsync(
            context,
            StatusCodes.Status401Unauthorized,
            "Unauthorized",
            "Authentication credentials missing or invalid. Provide an X-API-Key header or Bearer token.",
            ErrorCodes.Unauthorized);
    }

    private async Task HandlePortalUserAsync(
        HttpContext context,
        ICurrentProjectContextSetter projectContextSetter,
        ITenantResolutionResolver tenantResolutionResolver,
        ISecurityAuditService securityAuditService,
        TimeProvider timeProvider,
        string path)
    {
        var parsedUserId = ResolvePortalUserId(context.User);
        if (parsedUserId is { } uid &&
            !await tenantResolutionResolver.IsUserActiveAsync(uid, context.RequestAborted))
        {
            LogInactiveUserDenied(logger, uid.Value, path);
            await WriteProblemDetailsAsync(
                context,
                StatusCodes.Status401Unauthorized,
                "Unauthorized",
                "User account is inactive or revoked.",
                ErrorCodes.Unauthorized);
            return;
        }

        bool isSuperAdmin = IsSuperAdminClaim(context.User);
        var parsedSystemRole = isSuperAdmin ? SystemRole.SuperAdmin : SystemRole.Member;
        string actorIdentity = parsedUserId is { } actorUid ? $"User:{actorUid.Value:N}" : "portal";

        projectContextSetter.SetActor(
            userId: parsedUserId,
            systemRole: parsedSystemRole,
            callerIdentity: actorIdentity);

        if (isSuperAdmin && context.User.Identity is ClaimsIdentity superIdentity)
        {
            EnsureRoleClaim(superIdentity, RoleConstants.SuperAdmin);
        }

        if (IsPortalSelfServiceOrAuthPath(path))
        {
            projectContextSetter.SetControlPlane(actorIdentity);
            await next(context);
            return;
        }

        if (IsManagementPath(path))
        {
            bool authorized = await AuthorizeManagementRouteAsync(
                context,
                projectContextSetter,
                tenantResolutionResolver,
                parsedUserId,
                parsedSystemRole,
                actorIdentity,
                isSuperAdmin);
            if (!authorized)
            {
                return;
            }

            await next(context);
            return;
        }

        bool dataPlaneAuthorized = await AuthorizeDataPlaneUserAsync(
            context,
            projectContextSetter,
            tenantResolutionResolver,
            securityAuditService,
            timeProvider,
            parsedUserId,
            parsedSystemRole,
            actorIdentity,
            isSuperAdmin,
            path);

        if (dataPlaneAuthorized)
        {
            await next(context);
        }
    }

    private async Task<bool> AuthorizeManagementRouteAsync(
        HttpContext context,
        ICurrentProjectContextSetter projectContextSetter,
        ITenantResolutionResolver tenantResolutionResolver,
        UserId? parsedUserId,
        SystemRole parsedSystemRole,
        string actorIdentity,
        bool isSuperAdmin)
    {
        Guid? routeProjectId = ExtractRouteProjectId(context);
        if (isSuperAdmin)
        {
            string requestPath = context.Request.Path.Value ?? string.Empty;
            if (routeProjectId.HasValue &&
                requestPath.Contains("/api-keys", StringComparison.OrdinalIgnoreCase))
            {
                projectContextSetter.SetProject(new ProjectId(routeProjectId.Value), actorIdentity);
                return true;
            }

            projectContextSetter.SetControlPlane(actorIdentity);
            return true;
        }

        projectContextSetter.SetControlPlane(actorIdentity);

        if (!routeProjectId.HasValue)
        {
            return true;
        }

        var targetProjectId = new ProjectId(routeProjectId.Value);
        var project = await tenantResolutionResolver.FindProjectAsync(targetProjectId, context.RequestAborted);
        if (project is null)
        {
            LogTenantNotFound(logger, targetProjectId);
            await WriteProblemDetailsAsync(
                context,
                StatusCodes.Status404NotFound,
                "Not Found",
                $"Project '{targetProjectId}' not found.",
                ErrorCodes.NotFound);
            return false;
        }

        Guid userIdValue = parsedUserId?.Value ?? Guid.Empty;
        var membership = await tenantResolutionResolver.FindMembershipAsync(
            targetProjectId,
            new UserId(userIdValue),
            context.RequestAborted);

        if (membership is null)
        {
            LogZeroStandingAccessDenied(logger, userIdValue, parsedSystemRole, targetProjectId);
            await WriteProblemDetailsAsync(
                context,
                StatusCodes.Status403Forbidden,
                "Forbidden",
                $"User does not have permission to access project '{targetProjectId}'.",
                ErrorCodes.CrossTenantAccessDenied);
            return false;
        }

        return true;
    }

    private async Task<bool> AuthorizeDataPlaneUserAsync(
        HttpContext context,
        ICurrentProjectContextSetter projectContextSetter,
        ITenantResolutionResolver tenantResolutionResolver,
        ISecurityAuditService securityAuditService,
        TimeProvider timeProvider,
        UserId? parsedUserId,
        SystemRole parsedSystemRole,
        string actorIdentity,
        bool isSuperAdmin,
        string path)
    {
        var resolved = await TryResolveDataPlaneProjectIdAsync(context, path);
        if (!resolved.HasValue)
        {
            return false;
        }

        var targetProjectId = new ProjectId(resolved.Value);
        var project = await tenantResolutionResolver.FindProjectAsync(targetProjectId, context.RequestAborted);
        if (project is null)
        {
            LogTenantNotFound(logger, targetProjectId);
            await WriteProblemDetailsAsync(
                context,
                StatusCodes.Status404NotFound,
                "Not Found",
                $"Project '{targetProjectId}' not found.",
                ErrorCodes.NotFound);
            return false;
        }

        Guid userIdValue = parsedUserId?.Value ?? Guid.Empty;
        if (!project.IsActive)
        {
            LogTenantInactiveForUser(logger, userIdValue, targetProjectId);
            await WriteProblemDetailsAsync(
                context,
                StatusCodes.Status403Forbidden,
                "Forbidden",
                $"Project '{targetProjectId}' is deactivated.",
                ErrorCodes.ProjectDeactivated);
            return false;
        }

        return await AuthorizeMemberOrBreakGlassAsync(
            context,
            projectContextSetter,
            tenantResolutionResolver,
            securityAuditService,
            timeProvider,
            targetProjectId,
            userIdValue,
            parsedSystemRole,
            actorIdentity,
            isSuperAdmin,
            path);
    }

    private async Task<bool> AuthorizeMemberOrBreakGlassAsync(
        HttpContext context,
        ICurrentProjectContextSetter projectContextSetter,
        ITenantResolutionResolver tenantResolutionResolver,
        ISecurityAuditService securityAuditService,
        TimeProvider timeProvider,
        ProjectId targetProjectId,
        Guid userIdValue,
        SystemRole parsedSystemRole,
        string actorIdentity,
        bool isSuperAdmin,
        string path)
    {
        var typedUserId = new UserId(userIdValue);
        var roleMapping = await tenantResolutionResolver.FindMembershipAsync(
            targetProjectId,
            typedUserId,
            context.RequestAborted);

        if (roleMapping is not null)
        {
            if (context.User.Identity is ClaimsIdentity memberIdentity)
            {
                EnsureRoleClaim(memberIdentity, roleMapping.RoleName);
            }

            projectContextSetter.SetProject(targetProjectId, actorIdentity);
            return true;
        }

        if (isSuperAdmin &&
            userIdValue != Guid.Empty &&
            await tenantResolutionResolver.HasActiveBreakGlassGrantAsync(
                typedUserId,
                targetProjectId,
                timeProvider.GetUtcNow(),
                context.RequestAborted))
        {
            securityAuditService.LogBreakGlassAccessUsed(userIdValue, targetProjectId.Value, path);
            if (context.User.Identity is ClaimsIdentity breakGlassIdentity)
            {
                EnsureRoleClaim(breakGlassIdentity, RoleConstants.Admin);
            }

            projectContextSetter.SetProject(targetProjectId, actorIdentity);
            return true;
        }

        LogZeroStandingAccessDenied(logger, userIdValue, parsedSystemRole, targetProjectId);
        await WriteProblemDetailsAsync(
            context,
            StatusCodes.Status403Forbidden,
            "Forbidden",
            isSuperAdmin
                ? "SuperAdmin data-plane access requires an explicit project role or active break-glass grant."
                : $"User does not have permission to access project '{targetProjectId}'.",
            ErrorCodes.CrossTenantAccessDenied);
        return false;
    }

    private async Task<Guid?> TryResolveDataPlaneProjectIdAsync(HttpContext context, string path)
    {
        Guid? routeProjectIdGuid = ExtractRouteProjectId(context);
        Guid? headerProjectIdGuid = ExtractHeaderProjectId(context);

        if (routeProjectIdGuid.HasValue && headerProjectIdGuid.HasValue && routeProjectIdGuid.Value != headerProjectIdGuid.Value)
        {
            LogTenantConflict(logger, routeProjectIdGuid.Value, headerProjectIdGuid.Value);
            await WriteProblemDetailsAsync(
                context,
                StatusCodes.Status400BadRequest,
                "Bad Request",
                $"Tenant conflict detected: Route specifies project '{routeProjectIdGuid.Value}', but Project-Id header specifies '{headerProjectIdGuid.Value}'.",
                ErrorCodes.ProjectContextMismatch);
            return null;
        }

        Guid? targetProjectIdGuid = routeProjectIdGuid ?? headerProjectIdGuid ?? ExtractClaimProjectId(context.User);
        if (!targetProjectIdGuid.HasValue)
        {
            LogPerimeterProjectRequired(logger, path);
            await WriteProblemDetailsAsync(
                context,
                StatusCodes.Status400BadRequest,
                "Bad Request",
                "Project context is required. Provide a Project-Id header or valid project claim.",
                ErrorCodes.ProjectContextRequired);
            return null;
        }

        return targetProjectIdGuid.Value;
    }

    private async Task HandleApiKeyAuthAsync(
        HttpContext context,
        ICurrentProjectContextSetter projectContextSetter,
        IUseCase<ValidateApiKeyQuery, ValidatedApiKeyResultDto?> validateApiKeyUseCase,
        string rawKey,
        string path)
    {
        Guid? routeProjectIdGuid = ExtractRouteProjectId(context);
        Guid? headerProjectIdGuid = ExtractHeaderProjectId(context);

        if (TryHandleMasterApiKey(context, projectContextSetter, rawKey, routeProjectIdGuid ?? headerProjectIdGuid))
        {
            await next(context);
            return;
        }

        var key = await TryValidateApiKeyAsync(context, validateApiKeyUseCase, rawKey, path);
        if (key is null)
        {
            return;
        }

        var spoofedProjectId = ResolveSpoofedProjectId(key.ProjectId, headerProjectIdGuid, routeProjectIdGuid);
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

        projectContextSetter.SetActor(apiKeyId: new ApiKeyId(key.Id), callerIdentity: key.CallerApp);
        projectContextSetter.SetProject(new ProjectId(key.ProjectId), key.CallerApp);
        context.User = BuildApiKeyPrincipal(key);

        await next(context);
    }

    private bool TryHandleMasterApiKey(
        HttpContext context,
        ICurrentProjectContextSetter projectContextSetter,
        string rawKey,
        Guid? masterProjId)
    {
        if (string.IsNullOrEmpty(_masterApiKey) ||
            !CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(rawKey),
                Encoding.UTF8.GetBytes(_masterApiKey)))
        {
            return false;
        }

        projectContextSetter.SetActor(callerIdentity: "master");
        if (masterProjId.HasValue)
        {
            projectContextSetter.SetProject(new ProjectId(masterProjId.Value), "master");
        }
        else
        {
            projectContextSetter.SetControlPlane("master");
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
        return true;
    }

    private async Task<ValidatedApiKeyResultDto?> TryValidateApiKeyAsync(
        HttpContext context,
        IUseCase<ValidateApiKeyQuery, ValidatedApiKeyResultDto?> validateApiKeyUseCase,
        string rawKey,
        string path)
    {
        ValidatedApiKeyResultDto? key;
        try
        {
            key = await validateApiKeyUseCase.ExecuteAsync(new ValidateApiKeyQuery(rawKey), context.RequestAborted);
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
            return null;
        }

        if (key is null)
        {
            string? clientIp = context.Connection.RemoteIpAddress?.ToString();
            LogInvalidApiKeyAttempt(logger, clientIp, path);
            await WriteProblemDetailsAsync(
                context,
                StatusCodes.Status401Unauthorized,
                "Unauthorized",
                "Invalid or inactive API Key.",
                ErrorCodes.Unauthorized);
        }

        return key;
    }

    private static UserId? ResolvePortalUserId(ClaimsPrincipal user)
    {
        var userIdStr = user.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? user.FindFirst("sub")?.Value;

        return Guid.TryParse(userIdStr, out var userIdGuid) && userIdGuid != Guid.Empty
            ? new UserId(userIdGuid)
            : null;
    }

    private static bool IsSuperAdminClaim(ClaimsPrincipal user)
    {
        var systemRoleClaim = user.FindFirst("SystemRole")?.Value
            ?? user.FindFirst(ClaimTypes.Role)?.Value;
        return string.Equals(systemRoleClaim, RoleConstants.SuperAdmin, StringComparison.OrdinalIgnoreCase);
    }

    private static Guid? ExtractClaimProjectId(ClaimsPrincipal user)
    {
        var claimProjectIdStr = user.FindFirst("ProjectId")?.Value;
        return Guid.TryParse(claimProjectIdStr, out var claimProjId) && claimProjId != Guid.Empty
            ? claimProjId
            : null;
    }

    private static Guid? ResolveSpoofedProjectId(
        Guid keyProjectId,
        Guid? headerProjectIdGuid,
        Guid? routeProjectIdGuid)
    {
        if (headerProjectIdGuid.HasValue && headerProjectIdGuid.Value != keyProjectId)
        {
            return headerProjectIdGuid;
        }

        if (routeProjectIdGuid.HasValue && routeProjectIdGuid.Value != keyProjectId)
        {
            return routeProjectIdGuid;
        }

        return null;
    }

    private static ClaimsPrincipal BuildApiKeyPrincipal(ValidatedApiKeyResultDto key)
    {
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
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "ApiKeyScheme"));
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
            dictVal is not null &&
            Guid.TryParse(dictVal.ToString(), out var dictProjId) &&
            dictProjId != Guid.Empty)
        {
            return dictProjId;
        }

        var segments = context.Request.Path.Value?.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments is not null)
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

    private static bool IsPortalSelfServiceOrAuthPath(string path) =>
        path.StartsWith("/api/v1/portal/", StringComparison.OrdinalIgnoreCase) ||
        path.Equals("/api/v1/portal", StringComparison.OrdinalIgnoreCase) ||
        path.StartsWith("/api/v1/auth/", StringComparison.OrdinalIgnoreCase);

    private static bool IsManagementPath(string path) =>
        !path.StartsWith("/api/v1/management/settings/fonts", StringComparison.OrdinalIgnoreCase) &&
        (path.StartsWith("/api/v1/management/", StringComparison.OrdinalIgnoreCase) ||
         path.Equals("/api/v1/management", StringComparison.OrdinalIgnoreCase));

    private static bool IsMutatingNonStatelessRequest(HttpRequest request, string requestPath)
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
                requestPath.StartsWith("/api/v1/documents/preview", StringComparison.OrdinalIgnoreCase)
                || requestPath.StartsWith("/api/v1/rendering/preview", StringComparison.OrdinalIgnoreCase)
                || requestPath.EndsWith("/validate", StringComparison.OrdinalIgnoreCase)
                || requestPath.EndsWith("/validate-payload", StringComparison.OrdinalIgnoreCase);

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
            instance: context.Request.Path,
            traceId: ProblemDetailsBuilder.ResolveTraceId(context),
            typeBaseUri: _problemTypeBaseUri);

        await context.Response.WriteAsJsonAsync(
            problemDetails,
            options: null,
            contentType: "application/problem+json",
            cancellationToken: context.RequestAborted);
    }
}
