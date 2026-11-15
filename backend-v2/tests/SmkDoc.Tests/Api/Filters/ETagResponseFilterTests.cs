using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using SmkDoc.Api.Common.Responses;
using SmkDoc.Api.Filters;
using SmkDoc.Api.OpenApi;
using SmkDoc.Application.Modules.Authoring.Templates.DTOs;
using SmkDoc.Application.Modules.IdentityAccess.Security.DTOs;
using SmkDoc.Application.Modules.IdentityAccess.Users.DTOs;
using SmkDoc.Application.Modules.TenantManagement.BreakGlass.DTOs;

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

    [Fact]
    public async Task OnActionExecutionAsync_WhenApiKeyResultDtoReturned_ShouldSetFormattedETagHeader()
    {
        var sut = CreateSut();
        var dto = new ApiKeyResultDto(Guid.NewGuid(), "Billing", "billing", true, null, DateTimeOffset.UtcNow, "ReadOnly", "smk_live_****abcd", Version: 3);
        var (context, next) = CreateFilterPipeline(new OkObjectResult(new ApiResponse<ApiKeyResultDto>(dto)));

        await sut.OnActionExecutionAsync(context, next);

        context.HttpContext.Response.Headers.ETag.ToString().Should().Be("\"v3\"");
    }

    [Fact]
    public async Task OnActionExecutionAsync_WhenUserResultDtoReturned_ShouldSetFormattedETagHeader()
    {
        var sut = CreateSut();
        var dto = new UserResultDto(Guid.NewGuid(), "user@smkdoc.com", "Somchai", "Jaidee", "Admin", true, DateTimeOffset.UtcNow, Version: 5);
        var (context, next) = CreateFilterPipeline(new OkObjectResult(new ApiResponse<UserResultDto>(dto)));

        await sut.OnActionExecutionAsync(context, next);

        context.HttpContext.Response.Headers.ETag.ToString().Should().Be("\"v5\"");
    }

    [Fact]
    public async Task OnActionExecutionAsync_WhenBreakGlassGrantResultDtoReturned_ShouldSetFormattedETagHeader()
    {
        var sut = CreateSut();
        var now = DateTimeOffset.UtcNow;
        var dto = new BreakGlassGrantResultDto(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Support", now, now.AddHours(1), null, true, Version: 2);
        var (context, next) = CreateFilterPipeline(new OkObjectResult(new ApiResponse<BreakGlassGrantResultDto>(dto)));

        await sut.OnActionExecutionAsync(context, next);

        context.HttpContext.Response.Headers.ETag.ToString().Should().Be("\"v2\"");
    }
}
