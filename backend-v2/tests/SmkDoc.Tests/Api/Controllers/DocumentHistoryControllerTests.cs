using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SmkDoc.Api.Common.Responses;
using SmkDoc.Api.Constants;
using SmkDoc.Api.Controllers.Rendering;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.Rendering.Documents.DTOs;
using SmkDoc.Application.Modules.Rendering.Documents.Queries.DownloadDocumentVersion;
using SmkDoc.Application.Modules.Rendering.Documents.Queries.GetDocumentVersions;

namespace SmkDoc.Tests.Api.Controllers;

public sealed class DocumentHistoryControllerTests
{
    private readonly IUseCase<GetDocumentVersionsQuery, List<DocumentVersionDto>> _getVersionsUseCase = Substitute.For<IUseCase<GetDocumentVersionsQuery, List<DocumentVersionDto>>>();
    private readonly IUseCase<DownloadDocumentVersionQuery, DownloadDocumentVersionResult> _downloadVersionUseCase = Substitute.For<IUseCase<DownloadDocumentVersionQuery, DownloadDocumentVersionResult>>();

    private DocumentHistoryController CreateSut()
    {
        return new DocumentHistoryController(
            _getVersionsUseCase,
            _downloadVersionUseCase)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };
    }

    [Fact]
    public void DocumentHistoryController_ShouldBeDecoratedWithSecurityAndRoutingAttributes()
    {
        var type = typeof(DocumentHistoryController);

        var authAttr = type.GetCustomAttribute<AuthorizeAttribute>();
        authAttr.Should().NotBeNull();
        authAttr!.Policy.Should().Be(SmkDocPolicies.CanGenerateDocuments);

        var routes = type.GetCustomAttributes<RouteAttribute>().Select(r => r.Template).ToList();
        routes.Should().ContainSingle().Which.Should().Be("api/v1/documents");
    }

    [Fact]
    public async Task GetVersions_ShouldReturnOkWithVersionsList()
    {
        // Arrange
        var sut = CreateSut();
        var versions = new List<DocumentVersionDto>
        {
            new(Guid.NewGuid(), Guid.NewGuid(), "INV-001", 1, Guid.NewGuid(), Guid.NewGuid(), "Initial", "admin", DateTimeOffset.UtcNow)
        };

        _getVersionsUseCase.ExecuteAsync(Arg.Any<GetDocumentVersionsQuery>(), Arg.Any<CancellationToken>())
            .Returns(versions);

        // Act
        var result = await sut.GetVersions("INV-001", CancellationToken.None);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var body = okResult.Value.Should().BeOfType<ApiResponse<IEnumerable<DocumentVersionDto>>>().Subject;
        body.Data.Should().HaveCount(1);
    }

    [Fact]
    public async Task DownloadVersion_ShouldReturnFileStreamResult()
    {
        // Arrange
        var sut = CreateSut();
        using var stream = new MemoryStream([1, 2, 3]);
        _downloadVersionUseCase.ExecuteAsync(Arg.Any<DownloadDocumentVersionQuery>(), Arg.Any<CancellationToken>())
            .Returns(new DownloadDocumentVersionResult(stream, "application/pdf", "INV-001_v1.pdf"));

        // Act
        var result = await sut.DownloadVersion("INV-001", 1, CancellationToken.None);

        // Assert
        var fileResult = result.Should().BeOfType<FileStreamResult>().Subject;
        fileResult.ContentType.Should().Be("application/pdf");
        fileResult.FileDownloadName.Should().Be("INV-001_v1.pdf");
    }
}
