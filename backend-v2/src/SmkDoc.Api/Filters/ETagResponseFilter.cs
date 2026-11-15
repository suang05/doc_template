using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using SmkDoc.Api.Common.Responses;
using SmkDoc.Api.OpenApi;
using SmkDoc.Application.Common.Interfaces;

namespace SmkDoc.Api.Filters;

/// <summary>
/// Global action filter that automatically emits a strong HTTP <c>ETag: "v{version}"</c> response header
/// on 2xx egress whenever the response payload (or <see cref="IApiResponse.Data"/>) implements <see cref="IHasVersion"/>.
/// </summary>
public sealed class ETagResponseFilter : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var executed = await next();

        if (executed.Exception is null &&
            executed.Result is ObjectResult { StatusCode: null or >= 200 and < 300, Value: { } value } &&
            !context.HttpContext.Response.Headers.ContainsKey(OpenApiNames.ETagHeader))
        {
            var payload = value is IApiResponse envelope ? envelope.Data : value;
            if (payload is IHasVersion { Version: > 0 } versioned)
            {
                context.HttpContext.Response.Headers.ETag = $"\"v{versioned.Version}\"";
            }
        }
    }
}
