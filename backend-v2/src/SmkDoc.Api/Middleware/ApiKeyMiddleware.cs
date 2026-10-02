using SmkDoc.Api.Models;
using SmkDoc.Application.Modules.IdentityAccess.Security.Queries.ValidateApiKey;

namespace SmkDoc.Api.Middleware;

public class ApiKeyMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ApiKeyMiddleware> _logger;
    private readonly string? _masterApiKey;

    public ApiKeyMiddleware(RequestDelegate next, ILogger<ApiKeyMiddleware> logger, IConfiguration configuration)
    {
        _next = next;
        _logger = logger;
        _masterApiKey = configuration["MASTER_API_KEY"] ?? configuration["Security:ApiKey"];
    }

    public async Task InvokeAsync(HttpContext context, ValidateApiKeyUseCase validateApiKeyUseCase, ExecutionContextImpl executionContext)
    {
        string path = context.Request.Path.Value?.ToLowerInvariant() ?? "";

        // Populate execution context IP & user-agent
        executionContext.ClientIp = context.Connection.RemoteIpAddress?.ToString();
        executionContext.UserAgent = context.Request.Headers.UserAgent.ToString();

        // Allow public routes
        if (path == "/" ||
            path == "/health" ||
            path.StartsWith("/swagger") ||
            path.StartsWith("/api/auth") ||
            path.StartsWith("/api/v1/schemas/validate") ||
            path.StartsWith("/api/schemas/validate") ||
            path.StartsWith("/api/documents/preview"))
        {
            await _next(context);
            return;
        }

        // Check X-API-Key header
        if (!context.Request.Headers.TryGetValue("X-API-Key", out var extractedApiKey) ||
            string.IsNullOrWhiteSpace(extractedApiKey))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsJsonAsync(new
            {
                type = "https://tools.ietf.org/html/rfc7807",
                title = "Unauthorized",
                status = StatusCodes.Status401Unauthorized,
                detail = "X-API-Key header is missing or empty."
            });
            return;
        }

        var rawKey = extractedApiKey.ToString();

        // Master key bypass — allows bootstrapping when no DB keys exist yet
        if (!string.IsNullOrEmpty(_masterApiKey) && rawKey == _masterApiKey)
        {
            executionContext.CallerApp = "master";
            await _next(context);
            return;
        }

        var key = await validateApiKeyUseCase.ExecuteAsync(new ValidateApiKeyQuery(rawKey));
        if (key == null)
        {
            _logger.LogWarning("Invalid API Key attempt from {IpAddress} to {Path}", executionContext.ClientIp, path);
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsJsonAsync(new
            {
                type = "https://tools.ietf.org/html/rfc7807",
                title = "Unauthorized",
                status = StatusCodes.Status401Unauthorized,
                detail = "Invalid or inactive API Key."
            });
            return;
        }

        executionContext.ApiKeyId = key.Id;
        executionContext.CallerApp = key.CallerApp;
        executionContext.ProjectId = key.ProjectId;

        await _next(context);

    }
}
