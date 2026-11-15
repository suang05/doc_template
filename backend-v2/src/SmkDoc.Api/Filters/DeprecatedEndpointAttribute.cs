using Microsoft.AspNetCore.Mvc.Filters;

namespace SmkDoc.Api.Filters;

/// <summary>
/// Emits RFC 8594 numeric <c>Deprecation</c>, <c>Sunset</c>, and RFC 8288 <c>Link</c> headers on legacy routes
/// retained during API coexistence, and signals OpenAPI transformers to mark the operation deprecated.
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = true)]
public sealed class DeprecatedEndpointAttribute(
    string? onlyWhenPathStartsWith = null,
    string? successorPath = null) : ActionFilterAttribute
{
    public const string SunsetHttpDate = "Tue, 30 Jun 2026 23:59:59 GMT";
    public const long DeprecationUnixTimestamp = 1782863999; // 2026-06-30T23:59:59Z
    public const string DeprecationHeaderValue = "@1782863999";
    public const string DefaultSunsetPolicyPath = "/docs/migrations/v2";

    public string? OnlyWhenPathStartsWith { get; } = onlyWhenPathStartsWith;
    public string? SuccessorPath { get; } = successorPath;

    public override void OnActionExecuting(ActionExecutingContext context)
    {
        if (MatchesPath(context.HttpContext.Request.Path.Value))
        {
            var configuration = context.HttpContext.RequestServices?.GetService<IConfiguration>();
            var sunsetPolicyUrl = configuration?["Deprecation:SunsetPolicyUrl"];

            var headers = context.HttpContext.Response.Headers;
            headers["Deprecation"] = DeprecationHeaderValue;
            headers["Sunset"] = SunsetHttpDate;
            headers["Link"] = BuildLinkHeaderValue(sunsetPolicyUrl);
        }

        base.OnActionExecuting(context);
    }

    public string BuildLinkHeaderValue(string? sunsetPolicyUrl = null)
    {
        var resolvedSunsetUrl = string.IsNullOrWhiteSpace(sunsetPolicyUrl)
            ? DefaultSunsetPolicyPath
            : sunsetPolicyUrl.Trim();
        var sunsetLink = $"<{resolvedSunsetUrl}>; rel=\"sunset\"";
        return string.IsNullOrWhiteSpace(SuccessorPath)
            ? sunsetLink
            : $"<{SuccessorPath}>; rel=\"successor-version\", {sunsetLink}";
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
