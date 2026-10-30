using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using SmkDoc.Api.Filters;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Domain.ValueObjects.Ids;
using SmkDoc.Tests.Common;

namespace SmkDoc.Tests.Api.Filters;

public sealed class IdempotencyFilterTests
{
    private readonly IIdempotencyStore _idempotencyStore = Substitute.For<IIdempotencyStore>();
    private readonly ICurrentProjectContext _projectContext = Substitute.For<ICurrentProjectContext>();
    private readonly ILogger<IdempotencyFilter> _logger = Substitute.For<ILogger<IdempotencyFilter>>();
    private readonly JsonSerializerOptions jsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    private IdempotencyFilter CreateSut(TimeSpan? completedTtl = null, bool mandatory = false)
    {
        return new IdempotencyFilter(
            _idempotencyStore,
            _projectContext,
            _logger,
            jsonOptions,
            completedTtl ?? TimeSpan.FromHours(24),
            mandatory);
    }

    private string ConfigureResolvedTenant(ProjectId? projectId = null)
    {
        var pid = projectId ?? ProjectId.New();
        _projectContext.IsResolved.Returns(true);
        _projectContext.ProjectId.Returns(pid);
        return pid.Value.ToString("N");
    }

    [Fact]
    public async Task OnActionExecutionAsync_WhenHeaderMissingAndOptIn_ProceedsNormally()
    {
        var sut = CreateSut(mandatory: false);
        var (context, next) = CreateActionContext();

        var executedContext = CreateExecutedContext(context, new OkResult());
        next.Invoke().Returns(Task.FromResult(executedContext));

        await sut.OnActionExecutionAsync(context, next);

        await next.Received(1).Invoke();
        context.Result.Should().BeNull();
        await _idempotencyStore.DidNotReceive().TryAcquireOrGetAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task OnActionExecutionAsync_WhenHeaderMissingAndMandatory_Returns400BadRequest()
    {
        var sut = CreateSut(mandatory: true);
        var (context, next) = CreateActionContext();

        await sut.OnActionExecutionAsync(context, next);

        await next.DidNotReceive().Invoke();
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
        var (context, next) = CreateActionContext(idempotencyKey: "test-key-1");
        var tenantKey = ConfigureResolvedTenant();
        var expectedCacheKey = $"idempotency:{tenantKey}:test-key-1";

        var inFlightRecord = new IdempotencyRecord(
            expectedCacheKey,
            "fingerprint-123",
            IdempotencyStatus.InFlight,
            null,
            null,
            null,
            TestConstants.BaselineTime);

        _idempotencyStore.TryAcquireOrGetAsync(
                expectedCacheKey,
                Arg.Any<string>(),
                Arg.Any<TimeSpan>(),
                Arg.Any<CancellationToken>())
            .Returns(IdempotencyAcquisitionResult.Conflict(inFlightRecord));

        await sut.OnActionExecutionAsync(context, next);

        await next.DidNotReceive().Invoke();
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
        var (context, next) = CreateActionContext(idempotencyKey: "test-key-2");
        var tenantKey = ConfigureResolvedTenant();
        var expectedCacheKey = $"idempotency:{tenantKey}:test-key-2";

        var fingerprint = RequestFingerprintCalculator.ComputeFingerprint(context, jsonOptions);
        var cachedJson = "{\"templateId\":\"456\",\"status\":\"active\"}";
        var cachedHeaders = new Dictionary<string, string> { ["Location"] = "/api/v1/templates/456" };

        var completedRecord = new IdempotencyRecord(
            expectedCacheKey,
            fingerprint,
            IdempotencyStatus.Completed,
            StatusCodes.Status201Created,
            cachedJson,
            cachedHeaders,
            TestConstants.BaselineTime);

        _idempotencyStore.TryAcquireOrGetAsync(
                expectedCacheKey,
                fingerprint,
                Arg.Any<TimeSpan>(),
                Arg.Any<CancellationToken>())
            .Returns(IdempotencyAcquisitionResult.Conflict(completedRecord));

        await sut.OnActionExecutionAsync(context, next);

        await next.DidNotReceive().Invoke();
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
        var (context, next) = CreateActionContext(idempotencyKey: "test-key-3");
        var tenantKey = ConfigureResolvedTenant();
        var expectedCacheKey = $"idempotency:{tenantKey}:test-key-3";

        var completedRecord = new IdempotencyRecord(
            expectedCacheKey,
            "different-hash-from-first-request",
            IdempotencyStatus.Completed,
            StatusCodes.Status200OK,
            "{}",
            null,
            TestConstants.BaselineTime);

        _idempotencyStore.TryAcquireOrGetAsync(
                expectedCacheKey,
                Arg.Any<string>(),
                Arg.Any<TimeSpan>(),
                Arg.Any<CancellationToken>())
            .Returns(IdempotencyAcquisitionResult.Conflict(completedRecord));

        await sut.OnActionExecutionAsync(context, next);

        await next.DidNotReceive().Invoke();
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
        var (context, next) = CreateActionContext(idempotencyKey: "fail-key");
        var tenantKey = ConfigureResolvedTenant();
        var expectedCacheKey = $"idempotency:{tenantKey}:fail-key";

        _idempotencyStore.TryAcquireOrGetAsync(
                expectedCacheKey,
                Arg.Any<string>(),
                Arg.Any<TimeSpan>(),
                Arg.Any<CancellationToken>())
            .Returns(IdempotencyAcquisitionResult.Acquired());

        next.Invoke().ThrowsAsync(new InvalidOperationException("DB timeout"));

        var act = async () => await sut.OnActionExecutionAsync(context, next);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("DB timeout");
        await _idempotencyStore.Received(1).RemoveAsync(expectedCacheKey, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task OnActionExecutionAsync_WhenExecutionReturns201Created_CachesResponseSuccessfully()
    {
        var sut = CreateSut();
        var (context, next) = CreateActionContext(idempotencyKey: "success-key");
        var tenantKey = ConfigureResolvedTenant();
        var expectedCacheKey = $"idempotency:{tenantKey}:success-key";

        _idempotencyStore.TryAcquireOrGetAsync(
                expectedCacheKey,
                Arg.Any<string>(),
                Arg.Any<TimeSpan>(),
                Arg.Any<CancellationToken>())
            .Returns(IdempotencyAcquisitionResult.Acquired());

        var responseData = new { id = "new-template", name = "Contract" };
        var executedContext = CreateExecutedContext(context, new CreatedAtActionResult("GetById", "Template", new { id = "new-template" }, responseData));
        next.Invoke().Returns(Task.FromResult(executedContext));

        await sut.OnActionExecutionAsync(context, next);

        await _idempotencyStore.Received(1).SaveCompletedAsync(
            expectedCacheKey,
            Arg.Any<string>(),
            StatusCodes.Status201Created,
            Arg.Is<string>(json => json.Contains("new-template")),
            Arg.Any<IReadOnlyDictionary<string, string>?>(),
            TimeSpan.FromHours(24),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task OnActionExecutionAsync_WhenExecutionReturns400BadRequest_RollsBackLock()
    {
        var sut = CreateSut();
        var (context, next) = CreateActionContext(idempotencyKey: "bad-request-key");
        var tenantKey = ConfigureResolvedTenant();
        var expectedCacheKey = $"idempotency:{tenantKey}:bad-request-key";

        _idempotencyStore.TryAcquireOrGetAsync(
                expectedCacheKey,
                Arg.Any<string>(),
                Arg.Any<TimeSpan>(),
                Arg.Any<CancellationToken>())
            .Returns(IdempotencyAcquisitionResult.Acquired());

        var executedContext = CreateExecutedContext(context, new BadRequestObjectResult(new { error = "invalid" }));
        next.Invoke().Returns(Task.FromResult(executedContext));

        await sut.OnActionExecutionAsync(context, next);

        await _idempotencyStore.Received(1).RemoveAsync(expectedCacheKey, Arg.Any<CancellationToken>());
        await _idempotencyStore.DidNotReceive().SaveCompletedAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int>(), Arg.Any<string>(), Arg.Any<IReadOnlyDictionary<string, string>?>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task OnActionExecutionAsync_DifferentTenantsWithSameIdempotencyKey_PartitionIntoDistinctCacheKeys()
    {
        var sut = CreateSut();
        var tenantA = ProjectId.New();
        var tenantB = ProjectId.New();

        _idempotencyStore.TryAcquireOrGetAsync(
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<TimeSpan>(),
                Arg.Any<CancellationToken>())
            .Returns(IdempotencyAcquisitionResult.Acquired());

        // Tenant A call
        ConfigureResolvedTenant(tenantA);
        var (ctxA, nextA) = CreateActionContext(idempotencyKey: "shared-key");
        nextA.Invoke().Returns(Task.FromResult(CreateExecutedContext(ctxA, new OkObjectResult(new { ok = true }))));
        await sut.OnActionExecutionAsync(ctxA, nextA);

        // Tenant B call
        ConfigureResolvedTenant(tenantB);
        var (ctxB, nextB) = CreateActionContext(idempotencyKey: "shared-key");
        nextB.Invoke().Returns(Task.FromResult(CreateExecutedContext(ctxB, new OkObjectResult(new { ok = true }))));
        await sut.OnActionExecutionAsync(ctxB, nextB);

        await _idempotencyStore.Received(1).TryAcquireOrGetAsync(
            $"idempotency:{tenantA.Value:N}:shared-key",
            Arg.Any<string>(),
            Arg.Any<TimeSpan>(),
            Arg.Any<CancellationToken>());
        await _idempotencyStore.Received(1).TryAcquireOrGetAsync(
            $"idempotency:{tenantB.Value:N}:shared-key",
            Arg.Any<string>(),
            Arg.Any<TimeSpan>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task OnActionExecutionAsync_WhenProjectUnresolved_PartitionsByUserIdFallback()
    {
        var sut = CreateSut();
        var userId = UserId.New();
        _projectContext.IsResolved.Returns(false);
        _projectContext.UserId.Returns(userId);

        _idempotencyStore.TryAcquireOrGetAsync(
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<TimeSpan>(),
                Arg.Any<CancellationToken>())
            .Returns(IdempotencyAcquisitionResult.Acquired());

        var (ctx, next) = CreateActionContext(idempotencyKey: "global-op-key");
        next.Invoke().Returns(Task.FromResult(CreateExecutedContext(ctx, new OkObjectResult(new { ok = true }))));

        await sut.OnActionExecutionAsync(ctx, next);

        await _idempotencyStore.Received(1).TryAcquireOrGetAsync(
            $"idempotency:{userId.Value:N}:global-op-key",
            Arg.Any<string>(),
            Arg.Any<TimeSpan>(),
            Arg.Any<CancellationToken>());
    }

    private static (ActionExecutingContext context, ActionExecutionDelegate next) CreateActionContext(
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

        var next = Substitute.For<ActionExecutionDelegate>();
        return (executingContext, next);
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
