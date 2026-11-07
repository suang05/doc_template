using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;
using SmkDoc.Api.Filters;

namespace SmkDoc.Api.OpenApi.Transformers;

/// <summary>
/// Operation-level OpenAPI contract: stable operationId, security override for public routes,
/// Idempotency-Key header, and RFC 9457 error responses.
/// </summary>
public sealed class SmkDocOperationTransformer : IOpenApiOperationTransformer
{
    private static readonly IReadOnlyDictionary<string, string> DefaultErrorDescriptions =
        new Dictionary<string, string>
        {
            ["401"] = "Unauthorized - missing or invalid credentials.",
            ["403"] = "Forbidden - authenticated but not permitted.",
            ["429"] = "Too Many Requests - rate limit exceeded (see Retry-After header).",
            ["500"] = "Internal Server Error."
        };

    public Task TransformAsync(
        OpenApiOperation operation,
        OpenApiOperationTransformerContext context,
        CancellationToken ct)
    {
        // Step 1: Identity
        ApplyOperationId(operation, context.Description);

        // Step 2: Security
        var isPublic = IsPublicRoute(context.Description);
        if (isPublic)
        {
            operation.Security = [];
        }

        // Step 3: Headers
        ApplyIdempotencyHeader(operation, context.Description);
        ApplyETagResponseHeader(operation);

        // Step 4: Error contract
        AddDefaultErrorResponses(operation, isPublic);
        ConvertErrorResponsesToProblemDetails(operation);

        return Task.CompletedTask;
    }

    private static void ApplyOperationId(OpenApiOperation operation, ApiDescription description)
    {
        var routeValues = description.ActionDescriptor.RouteValues;
        var hasController = routeValues.TryGetValue("controller", out var controller);
        var action = ResolveActionName(description.ActionDescriptor, routeValues);
        if (!hasController || action is null)
        {
            return;
        }

        operation.OperationId = OpenApiOperationIdFactory.Create(controller!, action);
    }

    /// <summary>
    /// MVC strips the "Async" suffix from the routed action name, which would make
    /// <c>Generate</c> and <c>GenerateAsync</c> collide - prefer the real method name.
    /// </summary>
    private static string? ResolveActionName(ActionDescriptor descriptor, IDictionary<string, string?> routeValues)
    {
        if (descriptor is ControllerActionDescriptor { MethodInfo: { } methodInfo })
        {
            return methodInfo.Name;
        }

        return routeValues.TryGetValue("action", out var action) ? action : null;
    }

    private static bool IsPublicRoute(ApiDescription description)
    {
        var path = "/" + (description.RelativePath ?? string.Empty).Trim('/');
        return OpenApiNames.PublicRoutes.Contains(path, StringComparer.OrdinalIgnoreCase);
    }

    private static void ApplyIdempotencyHeader(OpenApiOperation operation, ApiDescription description)
    {
        var idempotent = description.ActionDescriptor.EndpointMetadata
            .OfType<IdempotentAttribute>()
            .FirstOrDefault();
        if (idempotent is null)
        {
            return;
        }

        operation.Parameters ??= [];
        operation.Parameters.Add(new OpenApiParameter
        {
            Name = OpenApiNames.IdempotencyKeyHeader,
            In = ParameterLocation.Header,
            Required = idempotent.Mandatory,
            Description = $"Client-generated unique key making retries safe. Completed responses are replayed for {idempotent.TtlHours} hours.",
            Schema = new OpenApiSchema { Type = JsonSchemaType.String }
        });
    }

    private static void ApplyETagResponseHeader(OpenApiOperation operation)
    {
        var hasIfMatch = operation.Parameters?.Any(parameter =>
            parameter.In == ParameterLocation.Header &&
            string.Equals(parameter.Name, OpenApiNames.IfMatchHeader, StringComparison.OrdinalIgnoreCase)) == true;
        if (!hasIfMatch || operation.Responses is null || !operation.Responses.TryGetValue("200", out var okResponse) || okResponse is not OpenApiResponse concreteResponse)
        {
            return;
        }

        concreteResponse.Headers ??= new Dictionary<string, IOpenApiHeader>();
        concreteResponse.Headers[OpenApiNames.ETagHeader] = new OpenApiHeader
        {
            Description = "Updated resource concurrency token (quoted rowversion).",
            Schema = new OpenApiSchema { Type = JsonSchemaType.String }
        };
    }

    private static void AddDefaultErrorResponses(OpenApiOperation operation, bool isPublic)
    {
        operation.Responses ??= [];
        foreach (var (statusCode, description) in DefaultErrorDescriptions)
        {
            var isAuthResponse = statusCode is "401" or "403";
            if (isPublic && isAuthResponse)
            {
                continue;
            }

            operation.Responses.TryAdd(statusCode, new OpenApiResponse { Description = description });
        }
    }

    private static void ConvertErrorResponsesToProblemDetails(OpenApiOperation operation)
    {
        var errorCodes = operation.Responses!.Keys.Where(IsErrorStatusCode).ToArray();
        foreach (var statusCode in errorCodes)
        {
            var existing = operation.Responses[statusCode];
            operation.Responses[statusCode] = new OpenApiResponse
            {
                Description = existing.Description,
                Headers = existing.Headers,
                Content = new Dictionary<string, OpenApiMediaType>
                {
                    [OpenApiNames.ProblemJsonMediaType] = new OpenApiMediaType
                    {
                        Schema = new OpenApiSchemaReference(OpenApiNames.ProblemDetailsSchema)
                    }
                }
            };
        }
    }

    private static bool IsErrorStatusCode(string statusCode) =>
        statusCode.Length == 3 && statusCode[0] is '4' or '5';
}
