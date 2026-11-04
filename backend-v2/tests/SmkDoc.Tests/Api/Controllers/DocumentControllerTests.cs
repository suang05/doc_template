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
using SmkDoc.Api.Filters;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.Authoring.Templates.Queries.ValidateTemplatePayload;
using SmkDoc.Application.Modules.Rendering.Documents.Commands.EnqueueDocumentGeneration;
using SmkDoc.Application.Modules.Rendering.Documents.Commands.GenerateDocument;
using SmkDoc.Application.Modules.Rendering.Documents.Commands.RenderStatelessDocument;
using SmkDoc.Application.Modules.Rendering.Documents.DTOs;
using SmkDoc.Application.Modules.Rendering.Documents.Queries.DownloadDocumentVersion;
using SmkDoc.Application.Modules.Rendering.Documents.Queries.GetDocumentJob;
using SmkDoc.Application.Modules.Rendering.Documents.Queries.GetDocumentVersions;
using SmkDoc.Application.Modules.Rendering.Documents.Queries.PreviewDocument;
using SmkDoc.Application.Modules.Rendering.Logs.Queries.DownloadGenerationLogDocument;
using SmkDoc.Application.Modules.Rendering.Logs.Queries.GetLogDownloadUrl;

namespace SmkDoc.Tests.Api.Controllers;

public sealed class DocumentControllerTests
{
    private readonly IUseCase<GenerateDocumentCommand, GenerateDocumentResultDto> _generateUseCase = Substitute.For<IUseCase<GenerateDocumentCommand, GenerateDocumentResultDto>>();
    private readonly IUseCase<PreviewDocumentQuery, Stream> _previewUseCase = Substitute.For<IUseCase<PreviewDocumentQuery, Stream>>();
    private readonly IUseCase<GetDocumentVersionsQuery, List<DocumentVersionDto>> _getVersionsUseCase = Substitute.For<IUseCase<GetDocumentVersionsQuery, List<DocumentVersionDto>>>();
    private readonly IUseCase<DownloadDocumentVersionQuery, DownloadDocumentVersionResult> _downloadVersionUseCase = Substitute.For<IUseCase<DownloadDocumentVersionQuery, DownloadDocumentVersionResult>>();
    private readonly IUseCase<GetLogDownloadUrlQuery, string> _getLogDownloadUrlUseCase = Substitute.For<IUseCase<GetLogDownloadUrlQuery, string>>();
    private readonly IUseCase<RenderStatelessDocumentCommand, Stream> _renderStatelessUseCase = Substitute.For<IUseCase<RenderStatelessDocumentCommand, Stream>>();
    private readonly IUseCase<ValidateTemplatePayloadQuery, ValidateTemplatePayloadResult> _validatePayloadUseCase = Substitute.For<IUseCase<ValidateTemplatePayloadQuery, ValidateTemplatePayloadResult>>();
    private readonly IUseCase<EnqueueDocumentGenerationCommand, DocumentJobDto> _enqueueJobUseCase = Substitute.For<IUseCase<EnqueueDocumentGenerationCommand, DocumentJobDto>>();
    private readonly IUseCase<GetDocumentJobQuery, DocumentJobDto> _getJobUseCase = Substitute.For<IUseCase<GetDocumentJobQuery, DocumentJobDto>>();
    private readonly IUseCase<DownloadGenerationLogDocumentQuery, DownloadGenerationLogDocumentResult> _downloadLogDocumentUseCase = Substitute.For<IUseCase<DownloadGenerationLogDocumentQuery, DownloadGenerationLogDocumentResult>>();

    private DocumentController CreateSut()
    {
        var controller = new DocumentController(
            _generateUseCase,
            _previewUseCase,
            _getVersionsUseCase,
            _downloadVersionUseCase,
            _getLogDownloadUrlUseCase,
            _renderStatelessUseCase,
            _validatePayloadUseCase,
            _enqueueJobUseCase,
            _getJobUseCase,
            _downloadLogDocumentUseCase)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };

        return controller;
    }

    [Fact]
    public void DocumentController_ShouldBeDecoratedWithSecurityAndRoutingAttributes()
    {
        var type = typeof(DocumentController);

        var authAttr = type.GetCustomAttribute<AuthorizeAttribute>();
        authAttr.Should().NotBeNull();
        authAttr!.Policy.Should().Be(SmkDocPolicies.CanGenerateDocuments);

        var routes = type.GetCustomAttributes<RouteAttribute>().Select(r => r.Template).ToList();
        routes.Should().ContainSingle().Which.Should().Be("api/v1/documents");
    }

    [Fact]
    public void CriticalEndpoints_ShouldBeDecoratedWithRateLimiting()
    {
        var type = typeof(DocumentController);

        var generateMethod = type.GetMethod(nameof(DocumentController.Generate));
        generateMethod!.GetCustomAttribute<EnableRateLimitingAttribute>()?.PolicyName.Should().Be("rendering");

        var generateAsyncMethod = type.GetMethod(nameof(DocumentController.GenerateAsync));
        generateAsyncMethod!.GetCustomAttribute<EnableRateLimitingAttribute>()?.PolicyName.Should().Be("rendering");

        var previewRawMethod = type.GetMethod(nameof(DocumentController.PreviewRaw));
        previewRawMethod!.GetCustomAttribute<EnableRateLimitingAttribute>()?.PolicyName.Should().Be("rendering");

        var previewBySlugMethod = type.GetMethod(nameof(DocumentController.PreviewBySlug));
        previewBySlugMethod!.GetCustomAttribute<EnableRateLimitingAttribute>()?.PolicyName.Should().Be("rendering");
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
        var result = await sut.GenerateAsync(request, "invoice", null, CancellationToken.None);

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
    public async Task DownloadByLogId_WhenAcceptHeaderIsJson_ShouldReturnPresignedUrlEnvelope()
    {
        // Arrange
        var sut = CreateSut();
        sut.Request.Headers.Accept = "application/json";
        var logId = Guid.NewGuid();

        _getLogDownloadUrlUseCase.ExecuteAsync(Arg.Is<GetLogDownloadUrlQuery>(q => q.LogId == logId), Arg.Any<CancellationToken>())
            .Returns("https://minio/outputs/doc.pdf");

        // Act
        var result = await sut.DownloadByLogId(logId, CancellationToken.None);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var body = okResult.Value.Should().BeOfType<ApiResponse<DownloadUrlResponseDto>>().Subject;
        body.Data.Url.Should().Be("https://minio/outputs/doc.pdf");
        body.Data.ExpiresInSeconds.Should().Be(3600);
    }

    [Fact]
    public async Task DownloadByLogId_WhenAcceptHeaderIsNotJson_ShouldStreamDirectFile()
    {
        // Arrange
        var sut = CreateSut();
        sut.Request.Headers.Accept = "application/pdf";
        var logId = Guid.NewGuid();
        using var stream = new MemoryStream([1, 2, 3]);

        _downloadLogDocumentUseCase.ExecuteAsync(Arg.Is<DownloadGenerationLogDocumentQuery>(q => q.LogId == logId), Arg.Any<CancellationToken>())
            .Returns(new DownloadGenerationLogDocumentResult(stream, "application/pdf", "sample.pdf"));

        // Act
        var result = await sut.DownloadByLogId(logId, CancellationToken.None);

        // Assert
        var fileResult = result.Should().BeOfType<FileStreamResult>().Subject;
        fileResult.ContentType.Should().Be("application/pdf");
        fileResult.FileDownloadName.Should().Be("sample.pdf");
        fileResult.FileStream.Should().BeSameAs(stream);
    }
}

