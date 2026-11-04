using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using SmkDoc.Api.Filters;

namespace SmkDoc.Tests.Api.Filters;

public sealed class DeprecatedRouteAttributeTests
{
    [Fact]
    public void OnActionExecuted_ShouldAppendDeprecationAndSunsetHeaders()
    {
        // Arrange
        var attribute = new DeprecatedRouteAttribute("/api/v1/management/api-keys", "2026-12-31T23:59:59Z");
        var httpContext = new DefaultHttpContext();
        var actionContext = new ActionContext(httpContext, new RouteData(), new ActionDescriptor());
        var context = new ActionExecutedContext(actionContext, new List<IFilterMetadata>(), Substitute.For<ControllerBase>());

        // Act
        attribute.OnActionExecuted(context);

        // Assert
        httpContext.Response.Headers.Should().ContainKey("Deprecation");
        httpContext.Response.Headers["Deprecation"].ToString().Should().Be("true");

        httpContext.Response.Headers.Should().ContainKey("Sunset");
        httpContext.Response.Headers["Sunset"].ToString().Should().Contain("2026");

        httpContext.Response.Headers.Should().ContainKey("Link");
        httpContext.Response.Headers["Link"].ToString().Should().Be("</api/v1/management/api-keys>; rel=\"successor-version\"");
    }
}

