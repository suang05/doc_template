using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SmkDoc.Api.Common.Responses;
using SmkDoc.Api.Constants;
using SmkDoc.Api.Contracts.Rendering.Documents;
using SmkDoc.Api.Controllers.Rendering;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.Rendering.Logs.DTOs;
using SmkDoc.Application.Modules.Rendering.Logs.Queries.DownloadGenerationLogDocument;
using SmkDoc.Application.Modules.Rendering.Logs.Queries.GetLogDownloadUrl;

namespace SmkDoc.Tests.Api.Controllers;

public sealed class DocumentArtifactControllerTests
{
    private readonly IUseCase<GetLogDownloadUrlQuery, DownloadUrlResultDto> _getLogDownloadUrlUseCase = Substitute.For<IUseCase<GetLogDownloadUrlQuery, DownloadUrlResultDto>>();
    private readonly IUseCase<DownloadGenerationLogDocumentQuery, DownloadGenerationLogDocumentResult> _downloadLogDocumentUseCase = Substitute.For<IUseCase<DownloadGenerationLogDocumentQuery, DownloadGenerationLogDocumentResult>>();

    private DocumentArtifactController CreateSut()
    {
        return new DocumentArtifactController(
            _getLogDownloadUrlUseCase,
            _downloadLogDocumentUseCase)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };
    }

    [Fact]
    public void DocumentArtifactController_ShouldBeDecoratedWithSecurityAndRoutingAttributes()
    {
        var type = typeof(DocumentArtifactController);

        var authAttr = type.GetCustomAttribute<AuthorizeAttribute>();
        authAttr.Should().NotBeNull();
        authAttr!.Policy.Should().Be(AuthorizationPolicies.CanGenerateDocuments);

        var routes = type.GetCustomAttributes<RouteAttribute>().Select(r => r.Template).ToList();
        routes.Should().ContainSingle().Which.Should().Be("api/v1/documents");
    }

    [Fact]
    public async Task GetDownloadUrl_ShouldReturnPresignedUrlEnvelope()
    {
        // Arrange
        var sut = CreateSut();
        var logId = Guid.NewGuid();
        _getLogDownloadUrlUseCase.ExecuteAsync(Arg.Is<GetLogDownloadUrlQuery>(q => q.LogId == logId), Arg.Any<CancellationToken>())
            .Returns(new DownloadUrlResultDto("https://minio/outputs/doc.pdf", 3600));

        // Act
        var result = await sut.GetDownloadUrl(logId, CancellationToken.None);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var body = okResult.Value.Should().BeOfType<ApiResponse<DownloadUrlResultDto>>().Subject;
        body.Data.Url.Should().Be("https://minio/outputs/doc.pdf");
        body.Data.ExpiresInSeconds.Should().Be(3600);
    }

    [Fact]
    public async Task DownloadFile_ShouldReturnDirectFileStreamResult()
    {
        // Arrange
        var sut = CreateSut();
        var logId = Guid.NewGuid();
        using var stream = new MemoryStream([1, 2, 3]);

        _downloadLogDocumentUseCase.ExecuteAsync(Arg.Is<DownloadGenerationLogDocumentQuery>(q => q.LogId == logId), Arg.Any<CancellationToken>())
            .Returns(new DownloadGenerationLogDocumentResult(stream, "application/pdf", "sample.pdf"));

        // Act
        var result = await sut.DownloadFile(logId, CancellationToken.None);

        // Assert
        var fileResult = result.Should().BeOfType<FileStreamResult>().Subject;
        fileResult.ContentType.Should().Be("application/pdf");
        fileResult.FileDownloadName.Should().Be("sample.pdf");
        fileResult.FileStream.Should().BeSameAs(stream);
    }

    [Fact]
    public async Task DownloadByLogId_WhenAcceptHeaderIsJson_ShouldReturnPresignedUrlEnvelope()
    {
        // Arrange
        var sut = CreateSut();
        sut.Request.Headers.Accept = "application/json";
        var logId = Guid.NewGuid();

        _getLogDownloadUrlUseCase.ExecuteAsync(Arg.Is<GetLogDownloadUrlQuery>(q => q.LogId == logId), Arg.Any<CancellationToken>())
            .Returns(new DownloadUrlResultDto("https://minio/outputs/doc.pdf", 3600));

        // Act
        var result = await sut.DownloadByLogId(logId, CancellationToken.None);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var body = okResult.Value.Should().BeOfType<ApiResponse<DownloadUrlResultDto>>().Subject;
        body.Data.Url.Should().Be("https://minio/outputs/doc.pdf");
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
    }
}

