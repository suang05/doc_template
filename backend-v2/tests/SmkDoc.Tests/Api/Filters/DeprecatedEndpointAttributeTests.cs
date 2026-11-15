using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using SmkDoc.Api.Filters;

namespace SmkDoc.Tests.Api.Filters;

public sealed class DeprecatedEndpointAttributeTests
{
    private static ActionExecutingContext CreateActionExecutingContext(string path)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Path = path;
        var actionContext = new ActionContext(httpContext, new RouteData(), new ActionDescriptor());
        return new ActionExecutingContext(actionContext, [], new Dictionary<string, object?>(), new object());
    }

    [Fact]
    public void OnActionExecuting_WhenSuccessorConfigured_EmitsRfc8594NumericDeprecationSunsetAndLinkHeaders()
    {
        // Arrange
        var sut = new DeprecatedEndpointAttribute(onlyWhenPathStartsWith: "/api/v1/fonts", successorPath: "/api/v1/portal/fonts");
        var context = CreateActionExecutingContext("/api/v1/fonts/upload");

        // Act
        sut.OnActionExecuting(context);

        // Assert
        var headers = context.HttpContext.Response.Headers;
        headers["Deprecation"].ToString().Should().Be("@1782863999");
        headers["Sunset"].ToString().Should().Be("Tue, 30 Jun 2026 23:59:59 GMT");
        headers["Link"].ToString().Should().Be(
            "</api/v1/portal/fonts>; rel=\"successor-version\", </docs/migrations/v2>; rel=\"sunset\"");
    }

    [Fact]
    public void OnActionExecuting_WithoutSuccessor_EmitsOnlySunsetRelationInLinkHeader()
    {
        // Arrange
        var sut = new DeprecatedEndpointAttribute();
        var context = CreateActionExecutingContext("/api/v1/legacy");

        // Act
        sut.OnActionExecuting(context);

        // Assert
        var headers = context.HttpContext.Response.Headers;
        headers["Deprecation"].ToString().Should().Be("@1782863999");
        headers["Sunset"].ToString().Should().Be("Tue, 30 Jun 2026 23:59:59 GMT");
        headers["Link"].ToString().Should().Be("</docs/migrations/v2>; rel=\"sunset\"");
    }

    [Fact]
    public void OnActionExecuting_WhenPathDoesNotMatchPrefix_DoesNotEmitDeprecationHeaders()
    {
        // Arrange
        var sut = new DeprecatedEndpointAttribute(onlyWhenPathStartsWith: "/api/v1/users", successorPath: "/api/v1/management/users");
        var context = CreateActionExecutingContext("/api/v1/management/users");

        // Act
        sut.OnActionExecuting(context);

        // Assert
        var headers = context.HttpContext.Response.Headers;
        headers.ContainsKey("Deprecation").Should().BeFalse();
        headers.ContainsKey("Sunset").Should().BeFalse();
        headers.ContainsKey("Link").Should().BeFalse();
    }
}
