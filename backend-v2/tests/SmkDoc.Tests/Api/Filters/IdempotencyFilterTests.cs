using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using Moq;
using SmkDoc.Api.Filters;
using SmkDoc.Application.Common.Interfaces;
using Xunit;

namespace SmkDoc.Tests.Api.Filters;

public class IdempotencyFilterTests
{
    private readonly Mock<IIdempotencyStore> idempotencyStoreMock = new();
    private readonly Mock<IExecutionContext> executionContextMock = new();
    private readonly Mock<ILogger<IdempotencyFilter>> loggerMock = new();
    private readonly JsonSerializerOptions jsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    private IdempotencyFilter CreateSut(TimeSpan? completedTtl = null, bool mandatory = false)
    {
        return new IdempotencyFilter(
            idempotencyStoreMock.Object,
            executionContextMock.Object,
            loggerMock.Object,
            jsonOptions,
            completedTtl ?? TimeSpan.FromHours(24),
            mandatory);
    }

    [Fact]
    public async Task OnActionExecutionAsync_WhenHeaderMissingAndOptIn_ProceedsNormally()
    {
        var sut = CreateSut(mandatory: false);
        var (context, nextMock) = CreateActionContext();

        var executedContext = CreateExecutedContext(context, new OkResult());
        nextMock.Setup(n => n()).ReturnsAsync(executedContext);

        await sut.OnActionExecutionAsync(context, nextMock.Object);

        nextMock.Verify(n => n(), Times.Once);
        context.Result.Should().BeNull();
        idempotencyStoreMock.Verify(s => s.TryAcquireOrGetAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task OnActionExecutionAsync_WhenHeaderMissingAndMandatory_Returns400BadRequest()
    {
        var sut = CreateSut(mandatory: true);
        var (context, nextMock) = CreateActionContext();

        await sut.OnActionExecutionAsync(context, nextMock.Object);

        nextMock.Verify(n => n(), Times.Never);
        context.Result.Should().BeOfType<BadRequestObjectResult>();
        var badRequest = (BadRequestObjectResult)context.Result!;
        var problem = badRequest.Value.Should().BeOfType<ProblemDetails>().Subject;
        problem.Status.Should().Be(StatusCodes.Status400BadRequest);
        problem.Extensions.Should().ContainKey("errorCode").WhoseValue.Should().Be("MISSING_IDEMPOTENCY_KEY");
    }

    [Fact]
    public async Task OnActionExecutionAsync_WhenKeyInFlight_Returns409Conflict()
    {
        var sut = CreateSut();
        var (context, nextMock) = CreateActionContext(idempotencyKey: "test-key-1");
        executionContextMock.Setup(e => e.CallerApp).Returns("erp-system");

        var inFlightRecord = new IdempotencyRecord(
            "idempotency:erp-system:test-key-1",
            "fingerprint-123",
            IdempotencyStatus.InFlight,
            null,
            null,
            null,
            DateTimeOffset.UtcNow);

        idempotencyStoreMock.Setup(s => s.TryAcquireOrGetAsync(
                "idempotency:erp-system:test-key-1",
                It.IsAny<string>(),
                It.IsAny<TimeSpan>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(IdempotencyAcquisitionResult.Conflict(inFlightRecord));

        await sut.OnActionExecutionAsync(context, nextMock.Object);

        nextMock.Verify(n => n(), Times.Never);
        context.Result.Should().BeOfType<ConflictObjectResult>();
        var conflict = (ConflictObjectResult)context.Result!;
        var problem = conflict.Value.Should().BeOfType<ProblemDetails>().Subject;
        problem.Status.Should().Be(StatusCodes.Status409Conflict);
        problem.Extensions.Should().ContainKey("errorCode").WhoseValue.Should().Be("IDEMPOTENCY_IN_FLIGHT");
    }

    [Fact]
    public async Task OnActionExecutionAsync_WhenCompletedWithSameFingerprint_ReplaysCachedContentWithHeader()
    {
        var sut = CreateSut();
        var (context, nextMock) = CreateActionContext(idempotencyKey: "test-key-2");
        executionContextMock.Setup(e => e.CallerApp).Returns("erp-system");

        var fingerprint = RequestFingerprintCalculator.ComputeFingerprint(context, jsonOptions);
        var cachedJson = "{\"templateId\":\"456\",\"status\":\"active\"}";
        var cachedHeaders = new Dictionary<string, string> { ["Location"] = "/api/v1/templates/456" };

        var completedRecord = new IdempotencyRecord(
            "idempotency:erp-system:test-key-2",
            fingerprint,
            IdempotencyStatus.Completed,
            StatusCodes.Status201Created,
            cachedJson,
            cachedHeaders,
            DateTimeOffset.UtcNow);

        idempotencyStoreMock.Setup(s => s.TryAcquireOrGetAsync(
                "idempotency:erp-system:test-key-2",
                fingerprint,
                It.IsAny<TimeSpan>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(IdempotencyAcquisitionResult.Conflict(completedRecord));

        await sut.OnActionExecutionAsync(context, nextMock.Object);

        nextMock.Verify(n => n(), Times.Never);
        context.Result.Should().BeOfType<ContentResult>();
        var contentResult = (ContentResult)context.Result!;
        contentResult.StatusCode.Should().Be(StatusCodes.Status201Created);
        contentResult.Content.Should().Be(cachedJson);
        contentResult.ContentType.Should().Be("application/json; charset=utf-8");

        context.HttpContext.Response.Headers.Should().ContainKey(IdempotencyFilter.HeaderReplayed);
        context.HttpContext.Response.Headers[IdempotencyFilter.HeaderReplayed].ToString().Should().Be("true");
        context.HttpContext.Response.Headers["Location"].ToString().Should().Be("/api/v1/templates/456");
    }

    [Fact]
    public async Task OnActionExecutionAsync_WhenCompletedWithMismatchedFingerprint_Returns422UnprocessableEntity()
    {
        var sut = CreateSut();
        var (context, nextMock) = CreateActionContext(idempotencyKey: "test-key-3");
        executionContextMock.Setup(e => e.CallerApp).Returns("erp-system");

        var completedRecord = new IdempotencyRecord(
            "idempotency:erp-system:test-key-3",
            "different-hash-from-first-request",
            IdempotencyStatus.Completed,
            StatusCodes.Status200OK,
            "{}",
            null,
            DateTimeOffset.UtcNow);

        idempotencyStoreMock.Setup(s => s.TryAcquireOrGetAsync(
                "idempotency:erp-system:test-key-3",
                It.IsAny<string>(),
                It.IsAny<TimeSpan>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(IdempotencyAcquisitionResult.Conflict(completedRecord));

        await sut.OnActionExecutionAsync(context, nextMock.Object);

        nextMock.Verify(n => n(), Times.Never);
        context.Result.Should().BeOfType<ObjectResult>();
        var objectResult = (ObjectResult)context.Result!;
        objectResult.StatusCode.Should().Be(StatusCodes.Status422UnprocessableEntity);
        var problem = objectResult.Value.Should().BeOfType<ProblemDetails>().Subject;
        problem.Extensions.Should().ContainKey("errorCode").WhoseValue.Should().Be("IDEMPOTENCY_PAYLOAD_MISMATCH");
    }

    [Fact]
    public async Task OnActionExecutionAsync_WhenExecutionThrowsException_RollsBackLockAndRethrows()
    {
        var sut = CreateSut();
        var (context, nextMock) = CreateActionContext(idempotencyKey: "fail-key");
        executionContextMock.Setup(e => e.CallerApp).Returns("erp-system");

        idempotencyStoreMock.Setup(s => s.TryAcquireOrGetAsync(
                "idempotency:erp-system:fail-key",
                It.IsAny<string>(),
                It.IsAny<TimeSpan>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(IdempotencyAcquisitionResult.Acquired());

        nextMock.Setup(n => n()).ThrowsAsync(new InvalidOperationException("DB timeout"));

        var act = async () => await sut.OnActionExecutionAsync(context, nextMock.Object);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("DB timeout");
        idempotencyStoreMock.Verify(s => s.RemoveAsync("idempotency:erp-system:fail-key", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task OnActionExecutionAsync_WhenExecutionReturns201Created_CachesResponseSuccessfully()
    {
        var sut = CreateSut();
        var (context, nextMock) = CreateActionContext(idempotencyKey: "success-key");
        executionContextMock.Setup(e => e.CallerApp).Returns("erp-system");

        idempotencyStoreMock.Setup(s => s.TryAcquireOrGetAsync(
                "idempotency:erp-system:success-key",
                It.IsAny<string>(),
                It.IsAny<TimeSpan>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(IdempotencyAcquisitionResult.Acquired());

        var responseData = new { id = "new-template", name = "Contract" };
        var executedContext = CreateExecutedContext(context, new CreatedAtActionResult("GetById", "Template", new { id = "new-template" }, responseData));
        nextMock.Setup(n => n()).ReturnsAsync(executedContext);

        await sut.OnActionExecutionAsync(context, nextMock.Object);

        idempotencyStoreMock.Verify(s => s.SaveCompletedAsync(
            "idempotency:erp-system:success-key",
            It.IsAny<string>(),
            StatusCodes.Status201Created,
            It.Is<string>(json => json.Contains("new-template")),
            It.IsAny<IReadOnlyDictionary<string, string>?>(),
            TimeSpan.FromHours(24),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task OnActionExecutionAsync_WhenExecutionReturns400BadRequest_RollsBackLock()
    {
        var sut = CreateSut();
        var (context, nextMock) = CreateActionContext(idempotencyKey: "bad-request-key");
        executionContextMock.Setup(e => e.CallerApp).Returns("erp-system");

        idempotencyStoreMock.Setup(s => s.TryAcquireOrGetAsync(
                "idempotency:erp-system:bad-request-key",
                It.IsAny<string>(),
                It.IsAny<TimeSpan>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(IdempotencyAcquisitionResult.Acquired());

        var executedContext = CreateExecutedContext(context, new BadRequestObjectResult(new { error = "invalid" }));
        nextMock.Setup(n => n()).ReturnsAsync(executedContext);

        await sut.OnActionExecutionAsync(context, nextMock.Object);

        idempotencyStoreMock.Verify(s => s.RemoveAsync("idempotency:erp-system:bad-request-key", It.IsAny<CancellationToken>()), Times.Once);
        idempotencyStoreMock.Verify(s => s.SaveCompletedAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<IReadOnlyDictionary<string, string>?>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private static (ActionExecutingContext context, Mock<ActionExecutionDelegate> nextMock) CreateActionContext(
        string? idempotencyKey = null,
        IDictionary<string, object?>? arguments = null)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Method = "POST";
        httpContext.Request.Path = "/api/v1/templates";

        if (!string.IsNullOrEmpty(idempotencyKey))
        {
            httpContext.Request.Headers[IdempotencyFilter.HeaderName] = idempotencyKey;
        }

        var actionContext = new ActionContext(
            httpContext,
            new RouteData(),
            new ActionDescriptor());

        var executingContext = new ActionExecutingContext(
            actionContext,
            new List<IFilterMetadata>(),
            arguments ?? new Dictionary<string, object?> { ["name"] = "Sample", ["slug"] = "sample-slug" },
            controller: new object());

        var nextMock = new Mock<ActionExecutionDelegate>();
        return (executingContext, nextMock);
    }

    private static ActionExecutedContext CreateExecutedContext(ActionExecutingContext executingContext, IActionResult result)
    {
        return new ActionExecutedContext(
            executingContext,
            executingContext.Filters,
            executingContext.Controller)
        {
            Result = result
        };
    }
}
