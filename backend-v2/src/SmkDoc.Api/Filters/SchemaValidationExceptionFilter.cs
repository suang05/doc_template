using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using SmkDoc.Domain.Exceptions;

namespace SmkDoc.Api.Filters;

/// <summary>
/// MVC Action Filter that catches <see cref="SchemaValidationException"/> and converts it
/// into an RFC 7807 Problem Details <c>400 Bad Request</c> response before the response
/// leaves the API layer — maintaining Clean Architecture boundaries.
/// </summary>
public sealed class SchemaValidationExceptionFilter : IExceptionFilter
{
    /// <inheritdoc />
    public void OnException(ExceptionContext context)
    {
        if (context.Exception is not SchemaValidationException ex)
            return;

        var errors = ex.Errors.Select(e => new
        {
            path        = e.PropertyPath,
            message     = e.Message,
            rule        = e.SchemaRule
        }).ToArray();

        var problemDetails = new
        {
            type     = "https://tools.ietf.org/html/rfc7807",
            title    = "Schema Validation Failed",
            status   = StatusCodes.Status400BadRequest,
            detail   = ex.Message,
            instance = $"/api/documents/generate/{ex.TemplateSlug}",
            errors
        };

        context.Result = new ObjectResult(problemDetails)
        {
            StatusCode = StatusCodes.Status400BadRequest
        };

        context.ExceptionHandled = true;
    }
}
