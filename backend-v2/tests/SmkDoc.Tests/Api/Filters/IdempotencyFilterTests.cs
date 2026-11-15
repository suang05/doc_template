using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using SmkDoc.Api.Filters;
using SmkDoc.Domain.ValueObjects.Ids;

namespace SmkDoc.Tests.Api.Filters;

public sealed class IdempotencyFilterTests
{
    private readonly IIdempotencyStore _idempotencyStore = Substitute.For<IIdempotencyStore>();
    private readonly IIdempotencyExecutionScope _executionScope = Substitute.For<IIdempotencyExecutionScope>();
    private readonly ICurrentProjectContext _projectContext = Substitute.For<ICurrentProjectContext>();
    private readonly ILogger<IdempotencyFilter> _logger = Substitute.For<ILogger<IdempotencyFilter>>();
    private readonly JsonSerializerOptions _jsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    private IdempotencyFilter CreateSut(TimeSpan? completedTtl = null, bool mandatory = false)
    {
        return new IdempotencyFilter(
            _idempotencyStore,
            _executionScope,
            _projectContext,
            _logger,
            _jsonOptions,
            completedTtl ?? TimeSpan.FromHours(24),
            mandatory);
    }

    private ProjectId ConfigureResolvedTenant(ProjectId? projectId = null)
    {
        var pid = projectId ?? ProjectId.New();
        var caller = $"project-{pid.Value:N}";
        _projectContext.IsResolved.Returns(true);
        _projectContext.GetRequiredProjectId().Returns(pid);
        _projectContext.TryGetProjectId().Returns(pid);
        _projectContext.ApiKeyId.Returns((ApiKeyId?)null);
        _projectContext.UserId.Returns((UserId?)null);
        _projectContext.CallerIdentity.Returns(caller);
        return pid;
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
        await _idempotencyStore.DidNotReceive().TryAcquireOrGetAsync(
            Arg.Any<IdempotencyAcquisitionRequest>(),
            Arg.Any<CancellationToken>());
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
        var projectId = ConfigureResolvedTenant();
        var cacheKey = $"{projectId.Value:N}:test-key-1";

        var inFlightRecord = new IdempotencyRecord(
            cacheKey,
            "fingerprint-123",
            IdempotencyStatus.InFlight,
            null,
            null,
            null,
            TestConstants.BaselineTime);

        _idempotencyStore.TryAcquireOrGetAsync(
                Arg.Is<IdempotencyAcquisitionRequest>(r =>
                    r.ProjectId == projectId.Value &&
                    r.IdempotencyKey == "test-key-1" &&
                    r.Scope == "POST:/api/v1/templates"),
                Arg.Any<CancellationToken>())
            .Returns(IdempotencyAcquisitionResult.Conflict(inFlightRecord));

        await sut.OnActionExecutionAsync(context, next);

        await next.DidNotReceive().Invoke();
        context.Result.Should().BeOfType<ConflictObjectResult>();
        var conflict = (ConflictObjectResult)context.Result!;
        var problem = conflict.Value.Should().BeOfType<ProblemDetails>().Subject;
        problem.Status.Should().Be(StatusCodes.Status409Conflict);
        problem.Extensions.Should().ContainKey("errorCode").WhoseValue.Should().Be("IDEMPOTENCY_IN_PROGRESS");
    }

    [Fact]
    public async Task OnActionExecutionAsync_WhenCompletedWithSameFingerprint_ReplaysCachedContentWithHeader()
    {
        var sut = CreateSut();
        var (context, next) = CreateActionContext(idempotencyKey: "test-key-2");
        var projectId = ConfigureResolvedTenant();
        var cacheKey = $"{projectId.Value:N}:test-key-2";

        var fingerprint = RequestFingerprintCalculator.ComputeFingerprint(context, _jsonOptions);
        var cachedJson = "{\"templateId\":\"456\",\"status\":\"active\"}";
        var cachedHeaders = new Dictionary<string, string> { ["Location"] = "/api/v1/templates/456" };

        var completedRecord = new IdempotencyRecord(
            cacheKey,
            fingerprint,
            IdempotencyStatus.Completed,
            StatusCodes.Status201Created,
            cachedJson,
            cachedHeaders,
            TestConstants.BaselineTime);

        _idempotencyStore.TryAcquireOrGetAsync(
                Arg.Is<IdempotencyAcquisitionRequest>(r =>
                    r.ProjectId == projectId.Value &&
                    r.IdempotencyKey == "test-key-2" &&
                    r.RequestFingerprint == fingerprint),
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
        var projectId = ConfigureResolvedTenant();
        var cacheKey = $"{projectId.Value:N}:test-key-3";

        var completedRecord = new IdempotencyRecord(
            cacheKey,
            "different-hash-from-first-request",
            IdempotencyStatus.Completed,
            StatusCodes.Status200OK,
            "{}",
            null,
            TestConstants.BaselineTime);

        _idempotencyStore.TryAcquireOrGetAsync(
                Arg.Is<IdempotencyAcquisitionRequest>(r =>
                    r.ProjectId == projectId.Value &&
                    r.IdempotencyKey == "test-key-3"),
                Arg.Any<CancellationToken>())
            .Returns(IdempotencyAcquisitionResult.Conflict(completedRecord));

        await sut.OnActionExecutionAsync(context, next);

        await next.DidNotReceive().Invoke();
        context.Result.Should().BeOfType<ObjectResult>();
        var objectResult = (ObjectResult)context.Result!;
        objectResult.StatusCode.Should().Be(StatusCodes.Status422UnprocessableEntity);
        var problem = objectResult.Value.Should().BeOfType<ProblemDetails>().Subject;
        problem.Extensions.Should().ContainKey("errorCode").WhoseValue.Should().Be("IDEMPOTENCY_KEY_REUSED");
    }

    [Fact]
    public async Task OnActionExecutionAsync_WhenExecutionThrowsException_RollsBackLockAndRethrows()
    {
        var sut = CreateSut();
        var (context, next) = CreateActionContext(idempotencyKey: "fail-key");
        var projectId = ConfigureResolvedTenant();

        _idempotencyStore.TryAcquireOrGetAsync(
                Arg.Is<IdempotencyAcquisitionRequest>(r =>
                    r.ProjectId == projectId.Value &&
                    r.IdempotencyKey == "fail-key"),
                Arg.Any<CancellationToken>())
            .Returns(IdempotencyAcquisitionResult.Acquired());

        next.Invoke().ThrowsAsync(new InvalidOperationException("DB timeout"));

        var act = async () => await sut.OnActionExecutionAsync(context, next);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("DB timeout");
        await _idempotencyStore.Received(1).RemoveAsync(
            projectId.Value,
            $"project-{projectId.Value:N}",
            "POST:/api/v1/templates",
            "fail-key",
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task OnActionExecutionAsync_WhenExecutionReturns201Created_CachesResponseSuccessfully()
    {
        var sut = CreateSut();
        var (context, next) = CreateActionContext(idempotencyKey: "success-key");
        var projectId = ConfigureResolvedTenant();

        _idempotencyStore.TryAcquireOrGetAsync(
                Arg.Is<IdempotencyAcquisitionRequest>(r =>
                    r.ProjectId == projectId.Value &&
                    r.IdempotencyKey == "success-key"),
                Arg.Any<CancellationToken>())
            .Returns(IdempotencyAcquisitionResult.Acquired());

        var responseData = new { id = "new-template", name = "Contract" };
        var executedContext = CreateExecutedContext(context, new CreatedAtActionResult("GetById", "Template", new { id = "new-template" }, responseData));
        next.Invoke().Returns(Task.FromResult(executedContext));

        await sut.OnActionExecutionAsync(context, next);

        _executionScope.Received(1).MarkFilterManaged();
        await _idempotencyStore.Received(1).SaveCompletedAsync(
            Arg.Is<IdempotencyCompletionRequest>(r =>
                r.ProjectId == projectId.Value &&
                r.IdempotencyKey == "success-key" &&
                r.StatusCode == StatusCodes.Status201Created &&
                r.ResponseJson.Contains("new-template") &&
                r.CompletedTtl == TimeSpan.FromHours(24)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task OnActionExecutionAsync_WhenExecutionReturns400BadRequest_RollsBackLock()
    {
        var sut = CreateSut();
        var (context, next) = CreateActionContext(idempotencyKey: "bad-request-key");
        var projectId = ConfigureResolvedTenant();

        _idempotencyStore.TryAcquireOrGetAsync(
                Arg.Is<IdempotencyAcquisitionRequest>(r =>
                    r.ProjectId == projectId.Value &&
                    r.IdempotencyKey == "bad-request-key"),
                Arg.Any<CancellationToken>())
            .Returns(IdempotencyAcquisitionResult.Acquired());

        var executedContext = CreateExecutedContext(context, new BadRequestObjectResult(new { error = "invalid" }));
        next.Invoke().Returns(Task.FromResult(executedContext));

        await sut.OnActionExecutionAsync(context, next);

        await _idempotencyStore.Received(1).RemoveAsync(
            projectId.Value,
            $"project-{projectId.Value:N}",
            "POST:/api/v1/templates",
            "bad-request-key",
            Arg.Any<CancellationToken>());
        await _idempotencyStore.DidNotReceive().SaveCompletedAsync(
            Arg.Any<IdempotencyCompletionRequest>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task OnActionExecutionAsync_DifferentTenantsWithSameIdempotencyKey_PartitionIntoDistinctRequests()
    {
        var sut = CreateSut();
        var tenantA = ProjectId.New();
        var tenantB = ProjectId.New();

        _idempotencyStore.TryAcquireOrGetAsync(
                Arg.Any<IdempotencyAcquisitionRequest>(),
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
            Arg.Is<IdempotencyAcquisitionRequest>(r =>
                r.ProjectId == tenantA.Value &&
                r.IdempotencyKey == "shared-key"),
            Arg.Any<CancellationToken>());
        await _idempotencyStore.Received(1).TryAcquireOrGetAsync(
            Arg.Is<IdempotencyAcquisitionRequest>(r =>
                r.ProjectId == tenantB.Value &&
                r.IdempotencyKey == "shared-key"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task OnActionExecutionAsync_WhenProjectUnresolved_PartitionsByUserIdFallback()
    {
        var sut = CreateSut();
        var userId = UserId.New();
        _projectContext.IsResolved.Returns(false);
        _projectContext.TryGetProjectId().Returns((ProjectId?)null);
        _projectContext.ApiKeyId.Returns((ApiKeyId?)null);
        _projectContext.UserId.Returns(userId);

        _idempotencyStore.TryAcquireOrGetAsync(
                Arg.Any<IdempotencyAcquisitionRequest>(),
                Arg.Any<CancellationToken>())
            .Returns(IdempotencyAcquisitionResult.Acquired());

        var (ctx, next) = CreateActionContext(idempotencyKey: "global-op-key");
        next.Invoke().Returns(Task.FromResult(CreateExecutedContext(ctx, new OkObjectResult(new { ok = true }))));

        await sut.OnActionExecutionAsync(ctx, next);

        await _idempotencyStore.Received(1).TryAcquireOrGetAsync(
            Arg.Is<IdempotencyAcquisitionRequest>(r =>
                r.ProjectId == null &&
                r.CredentialId == userId.Value.ToString("N") &&
                r.Scope == "POST:/api/v1/templates" &&
                r.IdempotencyKey == "global-op-key"),
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
