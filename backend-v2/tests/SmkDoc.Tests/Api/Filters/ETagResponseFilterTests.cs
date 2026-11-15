using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using SmkDoc.Api.Common.Responses;
using SmkDoc.Api.Filters;
using SmkDoc.Api.OpenApi;
using SmkDoc.Application.Modules.Authoring.Templates.DTOs;

namespace SmkDoc.Tests.Api.Filters;

public sealed class ETagResponseFilterTests
{
    private static ETagResponseFilter CreateSut() => new();

    private static (ActionExecutingContext ExecutingContext, ActionExecutionDelegate Next) CreateFilterPipeline(
        IActionResult result,
        string? preExistingEtag = null)
    {
        var httpContext = new DefaultHttpContext();
        if (preExistingEtag is not null)
        {
            httpContext.Response.Headers[OpenApiNames.ETagHeader] = preExistingEtag;
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
    public async Task OnActionExecutionAsync_WhenResponseWrapsIHasVersion_ShouldSetFormattedETagHeader()
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
            Version: 7);
        var envelope = new ApiResponse<TemplateResultDto>(dto);
        var (context, next) = CreateFilterPipeline(new OkObjectResult(envelope));

        // Step 2: Act
        await sut.OnActionExecutionAsync(context, next);

        // Step 3: Assert
        context.HttpContext.Response.Headers.ETag.ToString().Should().Be("\"v7\"");
    }

    [Fact]
    public async Task OnActionExecutionAsync_WhenSaveHtmlResultDtoReturned_ShouldSetAggregateVersionAsETagHeader()
    {
        // Step 1: Arrange
        var sut = CreateSut();
        var dto = new SaveHtmlResultDto(Version: 4, AggregateVersion: 9);
        var envelope = new ApiResponse<SaveHtmlResultDto>(dto);
        var (context, next) = CreateFilterPipeline(new OkObjectResult(envelope));

        // Step 2: Act
        await sut.OnActionExecutionAsync(context, next);

        // Step 3: Assert
        context.HttpContext.Response.Headers.ETag.ToString().Should().Be("\"v9\"");
    }

    [Fact]
    public async Task OnActionExecutionAsync_WhenETagHeaderAlreadySet_ShouldPreserveExistingHeader()
    {
        // Step 1: Arrange
        var sut = CreateSut();
        var dto = new SaveHtmlResultDto(Version: 4, AggregateVersion: 9);
        var (context, next) = CreateFilterPipeline(new OkObjectResult(dto), preExistingEtag: "\"v42\"");

        // Step 2: Act
        await sut.OnActionExecutionAsync(context, next);

        // Step 3: Assert
        context.HttpContext.Response.Headers.ETag.ToString().Should().Be("\"v42\"");
    }

    [Fact]
    public async Task OnActionExecutionAsync_WhenPayloadDoesNotImplementIHasVersion_ShouldNotEmitETag()
    {
        // Step 1: Arrange
        var sut = CreateSut();
        var (context, next) = CreateFilterPipeline(new OkObjectResult(new { Status = "ok" }));

        // Step 2: Act
        await sut.OnActionExecutionAsync(context, next);

        // Step 3: Assert
        context.HttpContext.Response.Headers.ContainsKey(OpenApiNames.ETagHeader).Should().BeFalse();
    }
}
