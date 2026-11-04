using System.Reflection;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SmkDoc.Api.Common.Responses;
using SmkDoc.Api.Constants;
using SmkDoc.Api.Contracts.Rendering.Documents;
using SmkDoc.Api.Controllers.Rendering;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.Rendering.Documents.Commands.EnqueueDocumentGeneration;
using SmkDoc.Application.Modules.Rendering.Documents.Commands.GenerateDocument;
using SmkDoc.Application.Modules.Rendering.Documents.DTOs;
using SmkDoc.Application.Modules.Rendering.Documents.Queries.GetDocumentJob;
using SmkDoc.Application.Modules.Rendering.Documents.Queries.PreviewDocument;

namespace SmkDoc.Tests.Api.Controllers;

public sealed class DocumentGenerationControllerTests
{
    private readonly IUseCase<GenerateDocumentCommand, GenerateDocumentResultDto> _generateUseCase = Substitute.For<IUseCase<GenerateDocumentCommand, GenerateDocumentResultDto>>();
    private readonly IUseCase<EnqueueDocumentGenerationCommand, DocumentJobDto> _enqueueJobUseCase = Substitute.For<IUseCase<EnqueueDocumentGenerationCommand, DocumentJobDto>>();
    private readonly IUseCase<GetDocumentJobQuery, DocumentJobDto> _getJobUseCase = Substitute.For<IUseCase<GetDocumentJobQuery, DocumentJobDto>>();
    private readonly IUseCase<PreviewDocumentQuery, Stream> _previewUseCase = Substitute.For<IUseCase<PreviewDocumentQuery, Stream>>();

    private DocumentGenerationController CreateSut()
    {
        return new DocumentGenerationController(
            _generateUseCase,
            _enqueueJobUseCase,
            _getJobUseCase,
            _previewUseCase)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };
    }

    [Fact]
    public void DocumentGenerationController_ShouldBeDecoratedWithSecurityAndRoutingAttributes()
    {
        var type = typeof(DocumentGenerationController);

        var authAttr = type.GetCustomAttribute<AuthorizeAttribute>();
        authAttr.Should().NotBeNull();
        authAttr!.Policy.Should().Be(SmkDocPolicies.CanGenerateDocuments);

        var routes = type.GetCustomAttributes<RouteAttribute>().Select(r => r.Template).ToList();
        routes.Should().ContainSingle().Which.Should().Be("api/v1/documents");
    }

    [Fact]
    public void CriticalEndpoints_ShouldBeDecoratedWithRateLimiting()
    {
        var type = typeof(DocumentGenerationController);

        var generateMethod = type.GetMethod(nameof(DocumentGenerationController.Generate));
        generateMethod!.GetCustomAttribute<EnableRateLimitingAttribute>()?.PolicyName.Should().Be("rendering");

        var generateAsyncMethod = type.GetMethod(nameof(DocumentGenerationController.GenerateAsync));
        generateAsyncMethod!.GetCustomAttribute<EnableRateLimitingAttribute>()?.PolicyName.Should().Be("rendering");

        var previewRawMethod = type.GetMethod(nameof(DocumentGenerationController.PreviewRaw));
        previewRawMethod!.GetCustomAttribute<EnableRateLimitingAttribute>()?.PolicyName.Should().Be("rendering");

        var previewBySlugMethod = type.GetMethod(nameof(DocumentGenerationController.PreviewBySlug));
        previewBySlugMethod!.GetCustomAttribute<EnableRateLimitingAttribute>()?.PolicyName.Should().Be("rendering");
    }

    [Fact]
    public async Task Generate_ShouldReturnOkWithResult()
    {
        // Arrange
        var sut = CreateSut();
        var expectedDto = new GenerateDocumentResultDto(
            Guid.NewGuid(), "INV-001", "pdf", 1024, "hash", "https://url", DateTimeOffset.UtcNow.AddHours(24));

        _generateUseCase.ExecuteAsync(Arg.Any<GenerateDocumentCommand>(), Arg.Any<CancellationToken>())
            .Returns(expectedDto);

        var request = new GenerateDocumentRequest(JsonDocument.Parse("{}").RootElement);

        // Act
        var result = await sut.Generate("invoice", request, CancellationToken.None);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var body = okResult.Value.Should().BeOfType<ApiResponse<GenerateDocumentResultDto>>().Subject;
        body.Data.DocumentRef.Should().Be("INV-001");
    }

    [Fact]
    public async Task GenerateAsync_ShouldReturn202AcceptedWithLocationHeader()
    {
        // Arrange
        var sut = CreateSut();
        var jobId = Guid.NewGuid();
        var jobDto = new DocumentJobDto(
            jobId,
            "invoice",
            "Queued",
            DateTimeOffset.UtcNow,
            null,
            null,
            $"/api/v1/documents/jobs/{jobId}",
            null,
            null,
            null);

        _enqueueJobUseCase.ExecuteAsync(Arg.Any<EnqueueDocumentGenerationCommand>(), Arg.Any<CancellationToken>())
            .Returns(jobDto);

        var request = new GenerateDocumentRequest(JsonDocument.Parse("{}").RootElement);

        // Act
        var result = await sut.GenerateAsync("invoice", request, CancellationToken.None);

        // Assert
        var objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(StatusCodes.Status202Accepted);
        sut.Response.Headers.Location.ToString().Should().Be($"/api/v1/documents/jobs/{jobId}");

        var body = objectResult.Value.Should().BeOfType<ApiResponse<DocumentJobDto>>().Subject;
        body.Data.JobId.Should().Be(jobId);
    }

    [Fact]
    public async Task GetJobStatus_ShouldReturn200OkWithJobEnvelope()
    {
        // Arrange
        var sut = CreateSut();
        var jobId = Guid.NewGuid();
        var jobDto = new DocumentJobDto(
            jobId,
            "invoice",
            "Completed",
            DateTimeOffset.UtcNow.AddMinutes(-1),
            DateTimeOffset.UtcNow.AddSeconds(-30),
            DateTimeOffset.UtcNow,
            $"/api/v1/documents/jobs/{jobId}",
            $"/api/v1/documents/download/{Guid.NewGuid()}",
            null,
            null);

        _getJobUseCase.ExecuteAsync(Arg.Any<GetDocumentJobQuery>(), Arg.Any<CancellationToken>())
            .Returns(jobDto);

        // Act
        var result = await sut.GetJobStatus(jobId, CancellationToken.None);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var body = okResult.Value.Should().BeOfType<ApiResponse<DocumentJobDto>>().Subject;
        body.Data.JobId.Should().Be(jobId);
        body.Data.Status.Should().Be("Completed");
    }

    [Fact]
    public async Task PreviewBySlug_ShouldReturnPdfFileResult()
    {
        // Arrange
        var sut = CreateSut();
        using var stream = new MemoryStream([1, 2, 3]);
        _previewUseCase.ExecuteAsync(Arg.Any<PreviewDocumentQuery>(), Arg.Any<CancellationToken>())
            .Returns(stream);

        var request = new PreviewDocumentRequest(JsonDocument.Parse("{}").RootElement);

        // Act
        var result = await sut.PreviewBySlug("invoice", request, CancellationToken.None);

        // Assert
        var fileResult = result.Should().BeOfType<FileStreamResult>().Subject;
        fileResult.ContentType.Should().Be("application/pdf");
        sut.Response.Headers.ContentDisposition.ToString().Should().Be("inline");
    }
}

