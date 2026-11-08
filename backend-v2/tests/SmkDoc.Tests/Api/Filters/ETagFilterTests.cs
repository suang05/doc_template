using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using SmkDoc.Api.Common.Responses;
using SmkDoc.Api.Filters;
using SmkDoc.Api.OpenApi;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.Authoring.Templates.DTOs;

namespace SmkDoc.Tests.Api.Filters;

public sealed class ETagFilterTests
{
    private readonly IConcurrencyContextSetter _concurrencySetter = Substitute.For<IConcurrencyContextSetter>();

    private ETagFilter CreateSut() => new(_concurrencySetter);

    private static (ActionExecutingContext ExecutingContext, ActionExecutionDelegate Next) CreateFilterPipeline(
        IActionResult result,
        string? ifMatchHeader = null)
    {
        var httpContext = new DefaultHttpContext();
        if (ifMatchHeader is not null)
        {
            httpContext.Request.Headers[OpenApiNames.IfMatchHeader] = ifMatchHeader;
        }

        var actionContext = new ActionContext(httpContext, new RouteData(), new ActionDescriptor());
        var executingContext = new ActionExecutingContext(
            actionContext,
            new List<IFilterMetadata>(),
            new Dictionary<string, object?>(),
            controller: new object());

        var executedContext = new ActionExecutedContext(
            actionContext,
            new List<IFilterMetadata>(),
            controller: new object())
        {
            Result = result
        };

        Task<ActionExecutedContext> Next() => Task.FromResult(executedContext);
        return (executingContext, Next);
    }

    [Fact]
    public async Task OnActionExecutionAsync_WhenIfMatchHeaderPresent_ShouldHydrateConcurrencyContextSetter()
    {
        // Step 1: Arrange
        var sut = CreateSut();
        var (context, next) = CreateFilterPipeline(new NoContentResult(), "\"123456\"");

        // Step 2: Act
        await sut.OnActionExecutionAsync(context, next);

        // Step 3: Assert
        _concurrencySetter.Received(1).SetIfMatch("\"123456\"");
    }

    [Fact]
    public async Task OnActionExecutionAsync_WhenResponseWrapsIHasConcurrencyToken_ShouldSetQuotedETagHeader()
    {
        // Step 1: Arrange
        var sut = CreateSut();
        var dto = new TemplateResultDto(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Invoice",
            "invoice",
            "Billing",
            true,
            null,
            "html",
            DateTimeOffset.UtcNow,
            null,
            "987654321");
        var envelope = new ApiResponse<TemplateResultDto>(dto);
        var (context, next) = CreateFilterPipeline(new OkObjectResult(envelope));

        // Step 2: Act
        await sut.OnActionExecutionAsync(context, next);

        // Step 3: Assert
        context.HttpContext.Response.Headers.ETag.ToString().Should().Be("\"987654321\"");
    }

    [Fact]
    public async Task OnActionExecutionAsync_WhenSaveHtmlResultDtoReturned_ShouldSetVersionAsETagHeader()
    {
        // Step 1: Arrange
        var sut = CreateSut();
        var dto = new SaveHtmlResultDto(4);
        var envelope = new ApiResponse<SaveHtmlResultDto>(dto);
        var (context, next) = CreateFilterPipeline(new OkObjectResult(envelope));

        // Step 2: Act
        await sut.OnActionExecutionAsync(context, next);

        // Step 3: Assert
        context.HttpContext.Response.Headers.ETag.ToString().Should().Be("\"4\"");
    }
}

