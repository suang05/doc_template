using Microsoft.AspNetCore.Mvc.Filters;

namespace SmkDoc.Api.Filters;

/// <summary>
/// Emits RFC 8594 <c>Deprecation: true</c> and <c>Sunset</c> response headers on legacy routes
/// retained during Phase 4 API coexistence, and signals OpenAPI transformers to mark the operation deprecated.
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = true)]
public sealed class DeprecatedEndpointAttribute(string? onlyWhenPathStartsWith = null) : ActionFilterAttribute
{
    public const string SunsetHttpDate = "Tue, 30 Jun 2026 23:59:59 GMT";

    public string? OnlyWhenPathStartsWith { get; } = onlyWhenPathStartsWith;

    public override void OnActionExecuting(ActionExecutingContext context)
    {
        if (MatchesPath(context.HttpContext.Request.Path.Value))
        {
            var headers = context.HttpContext.Response.Headers;
            headers["Deprecation"] = "true";
            headers["Sunset"] = SunsetHttpDate;
        }

        base.OnActionExecuting(context);
    }

    public bool MatchesPath(string? requestOrRelativePath)
    {
        if (string.IsNullOrWhiteSpace(OnlyWhenPathStartsWith))
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(requestOrRelativePath))
        {
            return false;
        }

        string normalized = requestOrRelativePath.StartsWith('/')
            ? requestOrRelativePath
            : "/" + requestOrRelativePath;

        return normalized.StartsWith(OnlyWhenPathStartsWith, StringComparison.OrdinalIgnoreCase);
    }
}
