using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging.Abstractions;
using SmkDoc.Api.Filters;
using SmkDoc.Domain.Exceptions;
using Xunit;

namespace SmkDoc.Tests.Api.Filters;

public class GlobalExceptionFilterTests
{
    [Theory]
    [InlineData(typeof(NotFoundException), 404, "RESOURCE_NOT_FOUND")]
    [InlineData(typeof(DraftExpiredException), 410, "DRAFT_EXPIRED")]
    [InlineData(typeof(ConflictException), 409, "RESOURCE_CONFLICT")]
    [InlineData(typeof(RenderException), 500, "DOCUMENT_RENDER_FAILED")]
    public void GlobalExceptionFilter_ShouldMapDomainExceptionToProblemDetails(Type exType, int expectedStatus, string expectedErrorCode)
    {
        DomainException ex = exType switch
        {
            _ when exType == typeof(NotFoundException) => new NotFoundException("Not found"),
            _ when exType == typeof(DraftExpiredException) => new DraftExpiredException("draft-1"),
            _ when exType == typeof(ConflictException) => new ConflictException("Conflict"),
            _ when exType == typeof(RenderException) => new RenderException("tpl", "engine", "crash"),
            _ => throw new NotImplementedException()
        };

        var filter = new GlobalExceptionFilter(NullLogger<GlobalExceptionFilter>.Instance);
        var actionContext = new ActionContext(new DefaultHttpContext(), new RouteData(), new ActionDescriptor());
        var context = new ExceptionContext(actionContext, new List<IFilterMetadata>())
        {
            Exception = ex
        };

        filter.OnException(context);

        context.ExceptionHandled.Should().BeTrue();
        context.Result.Should().BeOfType<ObjectResult>();
        var objResult = (ObjectResult)context.Result!;
        objResult.StatusCode.Should().Be(expectedStatus);

        var problemDetails = objResult.Value.Should().BeOfType<ProblemDetails>().Subject;
        problemDetails.Status.Should().Be(expectedStatus);
        problemDetails.Extensions.Should().ContainKey("errorCode");
        problemDetails.Extensions["errorCode"].Should().Be(expectedErrorCode);
    }

    [Fact]
    public void GlobalExceptionFilter_ShouldMapSchemaValidationException()
    {
        var errors = new List<SchemaValidationError>
        {
            new("/tax_id", "Invalid tax ID", "pattern")
        };
        var ex = new SchemaValidationException("tax-doc", 1, errors);

        var filter = new GlobalExceptionFilter(NullLogger<GlobalExceptionFilter>.Instance);
        var actionContext = new ActionContext(new DefaultHttpContext(), new RouteData(), new ActionDescriptor());
        var context = new ExceptionContext(actionContext, new List<IFilterMetadata>())
        {
            Exception = ex
        };

        filter.OnException(context);

        context.ExceptionHandled.Should().BeTrue();
        context.Result.Should().BeOfType<ObjectResult>();
        var objResult = (ObjectResult)context.Result!;
        objResult.StatusCode.Should().Be(400);
    }
}
