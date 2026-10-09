using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using SmkDoc.Api.ExceptionHandlers;
using SmkDoc.Application.Common.Exceptions;
using SmkDoc.Domain.Exceptions;
using Xunit;

namespace SmkDoc.Tests.Api.ExceptionHandlers;

public class GlobalExceptionHandlerTests
{
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
        httpContext.Request.Path = "/api/v1/templates/test";

        // Act
        var handled = await handler.TryHandleAsync(httpContext, ex, CancellationToken.None);

        // Assert
        handled.Should().BeTrue();
        httpContext.Response.StatusCode.Should().Be(expectedStatus);
        capturedProblemDetails.Should().NotBeNull();
        capturedProblemDetails!.Status.Should().Be(expectedStatus);
        capturedProblemDetails.Extensions.Should().ContainKey("errorCode");
        capturedProblemDetails.Extensions["errorCode"].Should().Be(expectedErrorCode);
        capturedProblemDetails.Type.Should().Be($"https://api.sammakorn.co.th/errors/{expectedErrorCode.ToLowerInvariant().Replace('_', '-')}");
        capturedProblemDetails.Instance.Should().Be("/api/v1/templates/test");
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
        httpContext.Request.Path = "/api/v1/documents/generate/tax-invoice";

        // Act
        var handled = await handler.TryHandleAsync(httpContext, ex, CancellationToken.None);

        // Assert
        handled.Should().BeTrue();
        httpContext.Response.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        capturedProblemDetails.Should().NotBeNull();
        capturedProblemDetails!.Status.Should().Be(StatusCodes.Status400BadRequest);
        capturedProblemDetails.Title.Should().Be("Schema Validation Failed");
        capturedProblemDetails.Extensions.Should().ContainKey("errorCode");
        capturedProblemDetails.Extensions["errorCode"].Should().Be("SCHEMA_VALIDATION_FAILED");
        capturedProblemDetails.Extensions.Should().ContainKey("errors");
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

        // Act
        var handled = await handler.TryHandleAsync(httpContext, ex, CancellationToken.None);

        // Assert
        handled.Should().BeTrue();
        httpContext.Response.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        capturedProblemDetails.Should().NotBeNull();
        capturedProblemDetails!.Status.Should().Be(StatusCodes.Status400BadRequest);
        capturedProblemDetails.Title.Should().Be("Validation Failed");
        capturedProblemDetails.Extensions["errorCode"].Should().Be("VALIDATION_FAILED");
        capturedProblemDetails.Extensions["errors"].Should().BeEquivalentTo(fieldErrors);
    }

    [Theory]
    [InlineData(typeof(UnauthorizedAccessException), StatusCodes.Status401Unauthorized, "UNAUTHORIZED")]
    [InlineData(typeof(ArgumentException), StatusCodes.Status400BadRequest, "BAD_REQUEST")]
    [InlineData(typeof(InvalidOperationException), StatusCodes.Status409Conflict, "INVALID_OPERATION")]
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

        // Act
        var handled = await handler.TryHandleAsync(httpContext, ex, CancellationToken.None);

        // Assert
        handled.Should().BeTrue();
        httpContext.Response.StatusCode.Should().Be(expectedStatus);
        capturedProblemDetails.Should().NotBeNull();
        capturedProblemDetails!.Status.Should().Be(expectedStatus);
        capturedProblemDetails.Extensions["errorCode"].Should().Be(expectedErrorCode);

        if (expectedStatus == StatusCodes.Status500InternalServerError)
        {
            capturedProblemDetails.Detail.Should().Be("An unexpected error occurred.");
        }
    }
}
