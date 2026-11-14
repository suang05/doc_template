using System.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using SmkDoc.Api.Common.Responses;
using SmkDoc.Api.ExceptionHandlers;

namespace SmkDoc.Tests.Api.ExceptionHandlers;

public sealed class GlobalExceptionHandlerTests
{
    private static async Task<(bool Handled, DefaultHttpContext HttpContext, ProblemDetails? ProblemDetails)> ExecuteHandlerAsync(
        Exception exception,
        string path = "/api/v1/templates/test",
        string traceId = "trace-001",
        IConfiguration? configuration = null,
        CancellationToken requestAborted = default)
    {
        ProblemDetails? capturedProblemDetails = null;
        var problemDetailsService = Substitute.For<IProblemDetailsService>();
        problemDetailsService
            .TryWriteAsync(Arg.Do<ProblemDetailsContext>(ctx => capturedProblemDetails = ctx.ProblemDetails))
            .Returns(ValueTask.FromResult(true));

        var handler = new GlobalExceptionHandler(
            NullLogger<GlobalExceptionHandler>.Instance,
            problemDetailsService,
            configuration);

        var httpContext = new DefaultHttpContext
        {
            TraceIdentifier = traceId,
            RequestAborted = requestAborted
        };
        httpContext.Request.Path = path;

        var handled = await handler.TryHandleAsync(httpContext, exception, CancellationToken.None);
        return (handled, httpContext, capturedProblemDetails);
    }

    [Theory]
    [InlineData(typeof(NotFoundException), StatusCodes.Status404NotFound, ErrorCodes.NotFound)]
    [InlineData(typeof(DraftExpiredException), StatusCodes.Status410Gone, ErrorCodes.DraftExpired)]
    [InlineData(typeof(ConflictException), StatusCodes.Status409Conflict, ErrorCodes.ResourceConflict)]
    [InlineData(typeof(BusinessRuleViolationException), StatusCodes.Status409Conflict, ErrorCodes.BusinessRuleViolation)]
    [InlineData(typeof(RenderException), StatusCodes.Status500InternalServerError, ErrorCodes.RenderFailed)]
    [InlineData(typeof(TenantContextNotResolvedException), StatusCodes.Status500InternalServerError, ErrorCodes.TenantContextNotResolved)]
    [InlineData(typeof(UnauthorizedException), StatusCodes.Status401Unauthorized, ErrorCodes.Unauthorized)]
    [InlineData(typeof(DomainValidationException), StatusCodes.Status422UnprocessableEntity, ErrorCodes.ValidationFailed)]
    public async Task TryHandleAsync_ShouldMapDomainExceptionToProblemDetails(Type exType, int expectedStatus, string expectedErrorCode)
    {
        // Arrange
        DomainException ex = exType switch
        {
            _ when exType == typeof(NotFoundException) => new NotFoundException("Template not found"),
            _ when exType == typeof(DraftExpiredException) => new DraftExpiredException("draft-123"),
            _ when exType == typeof(ConflictException) => new ConflictException("Slug already exists"),
            _ when exType == typeof(BusinessRuleViolationException) => new BusinessRuleViolationException("Cannot deactivate active default"),
            _ when exType == typeof(RenderException) => new RenderException("tpl", "engine", "sensitive internal stack"),
            _ when exType == typeof(TenantContextNotResolvedException) => new TenantContextNotResolvedException(),
            _ when exType == typeof(UnauthorizedException) => new UnauthorizedException("Access denied"),
            _ when exType == typeof(DomainValidationException) => new DomainValidationException("Invalid name"),
            _ => throw new NotImplementedException()
        };

        // Act
        var (handled, httpContext, problemDetails) = await ExecuteHandlerAsync(ex);

        // Assert
        handled.Should().BeTrue();
        httpContext.Response.StatusCode.Should().Be(expectedStatus);
        problemDetails.Should().NotBeNull();
        problemDetails!.Status.Should().Be(expectedStatus);
        problemDetails.Extensions.Should().ContainKey("errorCode").WhoseValue.Should().Be(expectedErrorCode);
        problemDetails.Extensions.Should().ContainKey("traceId").WhoseValue.Should().Be("trace-001");
        problemDetails.Type.Should().Be(ProblemDetailsBuilder.ResolveTypeUri(expectedErrorCode));
        problemDetails.Instance.Should().Be("/api/v1/templates/test");

        if (expectedStatus >= StatusCodes.Status500InternalServerError)
        {
            problemDetails.Detail.Should().Be("An unexpected error occurred.");
        }

        if (expectedStatus == StatusCodes.Status401Unauthorized)
        {
            httpContext.Response.Headers.WWWAuthenticate.ToString().Should().Be("Bearer");
        }
    }

    [Fact]
    public async Task TryHandleAsync_ShouldMapDbUpdateConcurrencyExceptionTo412PreconditionFailed()
    {
        // Arrange
        var ex = new DbUpdateConcurrencyException("Row was updated by another transaction.");

        // Act
        var (handled, httpContext, problemDetails) = await ExecuteHandlerAsync(ex, "/api/v1/templates/123", "trace-concurrency-99");

        // Assert
        handled.Should().BeTrue();
        httpContext.Response.StatusCode.Should().Be(StatusCodes.Status412PreconditionFailed);
        problemDetails.Should().NotBeNull();
        problemDetails!.Status.Should().Be(StatusCodes.Status412PreconditionFailed);
        problemDetails.Title.Should().Be("Precondition Failed");
        problemDetails.Detail.Should().Be("The resource was modified by another transaction while your request was processing. Please reload the latest state and retry.");
        problemDetails.Extensions["errorCode"].Should().Be(ErrorCodes.PreconditionFailed);
        problemDetails.Extensions["traceId"].Should().Be("trace-concurrency-99");
    }

    [Fact]
    public async Task TryHandleAsync_ShouldMapSchemaValidationExceptionWithRfc6901ErrorEntries()
    {
        // Arrange
        var errors = new List<SchemaValidationError>
        {
            new("$.customer.tax_id", "Invalid tax ID pattern", "pattern"),
            new("/amount", "Amount is required", "required")
        };
        var ex = new SchemaValidationException("tax-invoice", 2, errors);

        // Act
        var (handled, httpContext, problemDetails) = await ExecuteHandlerAsync(ex, "/api/v1/documents/generate/tax-invoice");

        // Assert
        handled.Should().BeTrue();
        httpContext.Response.StatusCode.Should().Be(StatusCodes.Status422UnprocessableEntity);
        problemDetails.Should().NotBeNull();
        problemDetails!.Status.Should().Be(StatusCodes.Status422UnprocessableEntity);
        problemDetails.Title.Should().Be("Schema Validation Failed");
        problemDetails.Extensions["errorCode"].Should().Be(ErrorCodes.SchemaValidationFailed);
        problemDetails.Extensions.Should().ContainKey("traceId");

        var entries = problemDetails.Extensions["errors"].Should().BeAssignableTo<IReadOnlyList<ValidationErrorEntry>>().Subject;
        entries.Should().BeEquivalentTo(
        [
            new ValidationErrorEntry("/customer/tax_id", "PATTERN", "Invalid tax ID pattern"),
            new ValidationErrorEntry("/amount", "REQUIRED", "Amount is required")
        ]);
    }

    [Fact]
    public async Task TryHandleAsync_ShouldMapValidationExceptionWithErrorEntries()
    {
        // Arrange
        var fieldErrors = new Dictionary<string, string[]>
        {
            ["Name"] = ["Name must not be empty.", "Name length exceeds 100."],
            ["Mappings[0].DatasetId"] = ["DatasetId is required."]
        };
        var ex = new ValidationException(fieldErrors);

        // Act
        var (handled, httpContext, problemDetails) = await ExecuteHandlerAsync(ex);

        // Assert
        handled.Should().BeTrue();
        httpContext.Response.StatusCode.Should().Be(StatusCodes.Status422UnprocessableEntity);
        problemDetails.Should().NotBeNull();
        problemDetails!.Status.Should().Be(StatusCodes.Status422UnprocessableEntity);
        problemDetails.Title.Should().Be("Validation Failed");
        problemDetails.Extensions["errorCode"].Should().Be(ErrorCodes.ValidationFailed);
        problemDetails.Extensions["traceId"].Should().Be("trace-001");

        var entries = problemDetails.Extensions["errors"].Should().BeAssignableTo<IReadOnlyList<ValidationErrorEntry>>().Subject;
        entries.Should().BeEquivalentTo(
        [
            new ValidationErrorEntry("/name", "VALIDATION_ERROR", "Name must not be empty."),
            new ValidationErrorEntry("/name", "VALIDATION_ERROR", "Name length exceeds 100."),
            new ValidationErrorEntry("/mappings/0/datasetId", "VALIDATION_ERROR", "DatasetId is required.")
        ]);
    }

    [Theory]
    [InlineData(typeof(UnauthorizedAccessException), StatusCodes.Status401Unauthorized, ErrorCodes.Unauthorized)]
    [InlineData(typeof(ArgumentException), StatusCodes.Status400BadRequest, ErrorCodes.MalformedRequest)]
    [InlineData(typeof(BadHttpRequestException), StatusCodes.Status400BadRequest, ErrorCodes.MalformedRequest)]
    [InlineData(typeof(ExternalServiceException), StatusCodes.Status503ServiceUnavailable, ErrorCodes.ExternalServiceUnavailable)]
    [InlineData(typeof(TimeoutException), StatusCodes.Status504GatewayTimeout, ErrorCodes.GatewayTimeout)]
    [InlineData(typeof(InvalidOperationException), StatusCodes.Status500InternalServerError, ErrorCodes.InternalError)]
    [InlineData(typeof(InvalidTimeZoneException), StatusCodes.Status500InternalServerError, ErrorCodes.InternalError)]
    public async Task TryHandleAsync_ShouldMapFrameworkAndFallbackExceptions(Type exType, int expectedStatus, string expectedErrorCode)
    {
        // Arrange
        Exception ex = exType switch
        {
            _ when exType == typeof(UnauthorizedAccessException) => new UnauthorizedAccessException("Forbidden token"),
            _ when exType == typeof(ArgumentException) => new ArgumentException("Missing parameter"),
            _ when exType == typeof(BadHttpRequestException) => new BadHttpRequestException("Invalid JSON body"),
            _ when exType == typeof(ExternalServiceException) => new ExternalServiceException("MinIO", "Connection refused"),
            _ when exType == typeof(TimeoutException) => new TimeoutException("Internal Gotenberg socket timed out"),
            _ when exType == typeof(InvalidOperationException) => new InvalidOperationException("Sequence contains no elements"),
            _ when exType == typeof(InvalidTimeZoneException) => new InvalidTimeZoneException("Unknown timezone"),
            _ => throw new NotImplementedException()
        };

        // Act
        var (handled, httpContext, problemDetails) = await ExecuteHandlerAsync(ex);

        // Assert
        handled.Should().BeTrue();
        httpContext.Response.StatusCode.Should().Be(expectedStatus);
        problemDetails.Should().NotBeNull();
        problemDetails!.Status.Should().Be(expectedStatus);
        problemDetails.Extensions.Should().ContainKey("errorCode").WhoseValue.Should().Be(expectedErrorCode);
        problemDetails.Extensions.Should().ContainKey("traceId").WhoseValue.Should().Be("trace-001");

        if (expectedStatus == StatusCodes.Status500InternalServerError)
        {
            problemDetails.Detail.Should().Be("An unexpected error occurred.");
        }
        else if (expectedStatus == StatusCodes.Status504GatewayTimeout)
        {
            problemDetails.Detail.Should().Be("The upstream operation timed out.");
        }
    }

    [Fact]
    public async Task TryHandleAsync_WhenActivityCurrentIsActive_ShouldPreferW3CTraceId()
    {
        // Arrange
        using var activity = new Activity("test-request");
        activity.Start();
        var ex = new NotFoundException("Missing template");

        // Act
        var (_, _, problemDetails) = await ExecuteHandlerAsync(ex, traceId: "aspnetcore-fallback");

        // Assert
        problemDetails!.Extensions["traceId"].Should().Be(activity.TraceId.ToHexString());
    }

    [Fact]
    public async Task TryHandleAsync_WhenProblemsBaseUriConfigured_ShouldUseConfiguredBaseUriForType()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Problems:BaseUri"] = "https://errors.smkdoc.example/problems/"
            })
            .Build();
        var ex = new DraftExpiredException("draft-99");

        // Act
        var (_, _, problemDetails) = await ExecuteHandlerAsync(ex, configuration: configuration);

        // Assert
        problemDetails!.Type.Should().Be("https://errors.smkdoc.example/problems/draft-expired");
    }

    [Fact]
    public async Task TryHandleAsync_WhenClientAbortedOperationCanceledException_ShouldReturnTrueWithoutProblemBody()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        var ex = new OperationCanceledException(cts.Token);

        // Act
        var (handled, httpContext, problemDetails) = await ExecuteHandlerAsync(ex, requestAborted: cts.Token);

        // Assert
        handled.Should().BeTrue();
        httpContext.Response.StatusCode.Should().Be(499);
        problemDetails.Should().BeNull();
    }
}
