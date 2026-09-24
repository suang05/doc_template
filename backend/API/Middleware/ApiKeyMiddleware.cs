using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using SmkDocServer.Domain.Interfaces;

namespace SmkDocServer.API.Middleware;

public class ApiKeyMiddleware
{
    private readonly RequestDelegate _next;
    private const string APIKEYNAME = "X-API-Key";

    public ApiKeyMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Allow Swagger, Downloads, Root static UI, OPTIONS preflight, and health checks without API Key
        if (context.Request.Method == HttpMethods.Options)
        {
            await _next(context);
            return;
        }

        var path = context.Request.Path;
        // Whitelist public endpoints (Root health status, Swagger docs, and OnlyOffice downloads/callbacks)
        if (!path.StartsWithSegments("/api") ||
            path.StartsWithSegments("/swagger") || 
            path.Value == "/" || 
            (path.Value != null && path.Value.EndsWith("/download", StringComparison.OrdinalIgnoreCase)) ||
            (path.Value != null && path.Value.Contains("/callback", StringComparison.OrdinalIgnoreCase)))
        {
            await _next(context);
            return;
        }

        string? extractedApiKey = null;
        if (context.Request.Headers.TryGetValue(APIKEYNAME, out var headerKey) && !string.IsNullOrWhiteSpace(headerKey))
        {
            extractedApiKey = headerKey.ToString();
        }
        else if (context.Request.Query.TryGetValue("apiKey", out var qApiKey) && !string.IsNullOrWhiteSpace(qApiKey))
        {
            extractedApiKey = qApiKey.ToString();
        }
        else if (context.Request.Query.TryGetValue("key", out var qKey) && !string.IsNullOrWhiteSpace(qKey))
        {
            extractedApiKey = qKey.ToString();
        }

        if (string.IsNullOrWhiteSpace(extractedApiKey))
        {
            context.Response.StatusCode = 401;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync("{\"error\":\"API Key was not provided. Please supply 'X-API-Key' header or '?apiKey=' query parameter.\"}");
            return;
        }

        var apiKeyService = context.RequestServices.GetRequiredService<IApiKeyService>();
        var authResult = await apiKeyService.ValidateApiKeyAsync(extractedApiKey.ToString());

        if (authResult == null)
        {
            context.Response.StatusCode = 401;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync("{\"error\":\"Unauthorized client. Invalid or revoked API Key.\"}");
            return;
        }

        // Attach Project and ApiKey to HttpContext.Items for downstream controllers and services
        context.Items["Project"] = authResult.Value.Project;
        context.Items["ApiKey"] = authResult.Value.ApiKey;

        await _next(context);
    }
}

