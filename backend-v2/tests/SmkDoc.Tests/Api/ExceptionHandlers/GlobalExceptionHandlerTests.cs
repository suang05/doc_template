using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using SmkDoc.Api.ExceptionHandlers;

namespace SmkDoc.Tests.Api.ExceptionHandlers;

public class GlobalExceptionHandlerTests
{
    private static async Task<(bool Handled, DefaultHttpContext HttpContext, ProblemDetails ProblemDetails)> ExecuteHandlerAsync(
        Exception exception,
        string path = "/api/v1/templates/test")
    {
        ProblemDetails? capturedProblemDetails = null;
        var problemDetailsServiceMock = new Mock<IProblemDetailsService>();
        problemDetailsServiceMock
            .Setup(s => s.TryWriteAsync(It.IsAny<ProblemDetailsContext>()))
            .Callback<ProblemDetailsContext>(ctx => capturedProblemDetails = ctx.ProblemDetails)
            .ReturnsAsync(true);

        var handler = new GlobalExceptionHandler(
            NullLogger<GlobalExceptionHandler>.Instance,
            problemDetailsServiceMock.Object);

        var httpContext = new DefaultHttpContext();
        httpContext.Request.Path = path;

        var handled = await handler.TryHandleAsync(httpContext, exception, CancellationToken.None);
        return (handled, httpContext, capturedProblemDetails!);
    }

    [Theory]
    [InlineData(typeof(NotFoundException), StatusCodes.Status404NotFound, "RESOURCE_NOT_FOUND")]
    [InlineData(typeof(DraftExpiredException), StatusCodes.Status410Gone, "DRAFT_EXPIRED")]
    [InlineData(typeof(ConflictException), StatusCodes.Status409Conflict, "RESOURCE_CONFLICT")]
    [InlineData(typeof(RenderException), StatusCodes.Status500InternalServerError, "DOCUMENT_RENDER_FAILED")]
    [InlineData(typeof(UnauthorizedException), StatusCodes.Status401Unauthorized, "UNAUTHORIZED")]
    [InlineData(typeof(DomainValidationException), StatusCodes.Status400BadRequest, "DOMAIN_VALIDATION_ERROR")]
    public async Task TryHandleAsync_ShouldMapDomainExceptionToProblemDetails(Type exType, int expectedStatus, string expectedErrorCode)
    {
        // Arrange
        DomainException ex = exType switch
        {
            _ when exType == typeof(NotFoundException) => new NotFoundException("Template not found"),
            _ when exType == typeof(DraftExpiredException) => new DraftExpiredException("draft-123"),
            _ when exType == typeof(ConflictException) => new ConflictException("Slug already exists"),
            _ when exType == typeof(RenderException) => new RenderException("tpl", "engine", "crash"),
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
        problemDetails.Status.Should().Be(expectedStatus);
        problemDetails.Extensions.Should().ContainKey("errorCode");
        problemDetails.Extensions["errorCode"].Should().Be(expectedErrorCode);
        problemDetails.Type.Should().Be($"https://api.sammakorn.co.th/errors/{expectedErrorCode.ToLowerInvariant().Replace('_', '-')}");
        problemDetails.Instance.Should().Be("/api/v1/templates/test");
    }

    [Fact]
    public async Task TryHandleAsync_ShouldMapSchemaValidationExceptionWithRichErrors()
    {
        // Arrange
        var errors = new List<SchemaValidationError>
        {
            new("/tax_id", "Invalid tax ID pattern", "pattern"),
            new("/amount", "Amount is required", "required")
        };
        var ex = new SchemaValidationException("tax-invoice", 2, errors);

        // Act
        var (handled, httpContext, problemDetails) = await ExecuteHandlerAsync(ex, "/api/v1/documents/generate/tax-invoice");

        // Assert
        handled.Should().BeTrue();
        httpContext.Response.StatusCode.Should().Be(StatusCodes.Status422UnprocessableEntity);
        problemDetails.Should().NotBeNull();
        problemDetails.Status.Should().Be(StatusCodes.Status422UnprocessableEntity);
        problemDetails.Title.Should().Be("Schema Validation Failed");
        problemDetails.Extensions.Should().ContainKey("errorCode");
        problemDetails.Extensions["errorCode"].Should().Be("SCHEMA_VALIDATION_FAILED");
        problemDetails.Extensions.Should().ContainKey("errors");
    }

    [Fact]
    public async Task TryHandleAsync_ShouldMapValidationExceptionWithDictionaryErrors()
    {
        // Arrange
        var fieldErrors = new Dictionary<string, string[]>
        {
            ["Name"] = ["Name must not be empty.", "Name length exceeds 100."],
            ["Slug"] = ["Slug is required."]
        };
        var ex = new ValidationException(fieldErrors);

        // Act
        var (handled, httpContext, problemDetails) = await ExecuteHandlerAsync(ex);

        // Assert
        handled.Should().BeTrue();
        httpContext.Response.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        problemDetails.Should().NotBeNull();
        problemDetails.Status.Should().Be(StatusCodes.Status400BadRequest);
        problemDetails.Title.Should().Be("Validation Failed");
        problemDetails.Extensions["errorCode"].Should().Be("VALIDATION_FAILED");
        problemDetails.Extensions["errors"].Should().BeEquivalentTo(fieldErrors);
    }

    [Theory]
    [InlineData(typeof(UnauthorizedAccessException), StatusCodes.Status401Unauthorized, "UNAUTHORIZED")]
    [InlineData(typeof(ArgumentException), StatusCodes.Status400BadRequest, "BAD_REQUEST")]
    [InlineData(typeof(InvalidOperationException), StatusCodes.Status500InternalServerError, "INTERNAL_SERVER_ERROR")]
    [InlineData(typeof(InvalidTimeZoneException), StatusCodes.Status500InternalServerError, "INTERNAL_SERVER_ERROR")]
    public async Task TryHandleAsync_ShouldMapFrameworkAndFallbackExceptions(Type exType, int expectedStatus, string expectedErrorCode)
    {
        // Arrange
        Exception ex = exType switch
        {
            _ when exType == typeof(UnauthorizedAccessException) => new UnauthorizedAccessException("Forbidden token"),
            _ when exType == typeof(ArgumentException) => new ArgumentException("Missing parameter"),
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
        problemDetails.Status.Should().Be(expectedStatus);
        problemDetails.Extensions["errorCode"].Should().Be(expectedErrorCode);

        if (expectedStatus == StatusCodes.Status500InternalServerError)
        {
            problemDetails.Detail.Should().Be("An unexpected error occurred.");
        }
    }
}
