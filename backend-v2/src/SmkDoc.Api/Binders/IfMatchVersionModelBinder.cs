using System.Globalization;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.Primitives;
using SmkDoc.Api.OpenApi;
using SmkDoc.Application.Common.Exceptions;

namespace SmkDoc.Api.Binders;

/// <summary>
/// Strictly parses strong ETag <c>If-Match: "v{version}"</c> headers, rejecting missing headers with HTTP 428
/// and weak (<c>W/</c>), wildcard (<c>*</c>), or malformed tags with HTTP 412.
/// </summary>
public sealed class IfMatchVersionModelBinder : IModelBinder
{
    public Task BindModelAsync(ModelBindingContext bindingContext)
    {
        ArgumentNullException.ThrowIfNull(bindingContext);

        if (!bindingContext.HttpContext.Request.Headers.TryGetValue(OpenApiNames.IfMatchHeader, out var values) ||
            StringValues.IsNullOrEmpty(values))
        {
            throw PreconditionFailedException.PreconditionRequired();
        }

        string raw = values.ToString().Trim();
        if (string.IsNullOrEmpty(raw))
        {
            throw PreconditionFailedException.PreconditionRequired();
        }

        if (!TryParseStrongVersionTag(raw, out long version) || version < 1)
        {
            throw PreconditionFailedException.VersionMismatch();
        }

        bindingContext.Result = ModelBindingResult.Success(version);
        return Task.CompletedTask;
    }

    private static bool TryParseStrongVersionTag(string headerValue, out long version)
    {
        version = 0;
        if (!headerValue.StartsWith("\"v", StringComparison.Ordinal) ||
            !headerValue.EndsWith('"') ||
            headerValue.Length <= 3)
        {
            return false;
        }

        var span = headerValue.AsSpan(2, headerValue.Length - 3);
        return long.TryParse(span, NumberStyles.None, CultureInfo.InvariantCulture, out version);
    }
}
