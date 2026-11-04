using System.Globalization;
using Microsoft.AspNetCore.Mvc.Filters;

namespace SmkDoc.Api.Filters;

/// <summary>
/// Action filter applying RFC 8594 Sunset and Deprecation HTTP response headers to legacy endpoints.
/// Informs external integration clients of route decommissioning deadlines and canonical successors.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public sealed class DeprecatedRouteAttribute : ActionFilterAttribute
{
    private readonly string _successorPath;
    private readonly DateTimeOffset _sunsetDate;

    /// <summary>
    /// Initializes a new instance of <see cref="DeprecatedRouteAttribute"/>.
    /// </summary>
    /// <param name="successorPath">Canonical successor URI path (e.g. "/api/v1/management/api-keys").</param>
    /// <param name="sunsetDateIso">Sunset date in ISO-8601 format (defaults to 2026-12-31T23:59:59Z).</param>
    public DeprecatedRouteAttribute(string successorPath, string sunsetDateIso = "2026-12-31T23:59:59Z")
    {
        _successorPath = successorPath;
        _sunsetDate = DateTimeOffset.Parse(sunsetDateIso, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal);
    }

    public override void OnActionExecuted(ActionExecutedContext context)
    {
        var response = context.HttpContext.Response;
        if (!response.HasStarted)
        {
            response.Headers["Deprecation"] = "true";
            response.Headers["Sunset"] = _sunsetDate.ToString("r", CultureInfo.InvariantCulture);

            if (!string.IsNullOrWhiteSpace(_successorPath))
            {
                response.Headers["Link"] = $"<{_successorPath}>; rel=\"successor-version\"";
            }
        }

        base.OnActionExecuted(context);
    }
}

