using System.Linq.Expressions;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.Rendering.Documents.Queries.DownloadDocumentVersion;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Enums;
using SmkDoc.Domain.Exceptions;
using SmkDoc.Tests.Common.Factories;

namespace SmkDoc.Tests.Application.Modules.Rendering.Documents.Queries.DownloadDocumentVersion;

public class DownloadDocumentVersionUseCaseTests
{
    private readonly Mock<IRepository<Document>> _documentRepoMock = new();
    private readonly Mock<IRepository<DocumentVersion>> _versionRepoMock = new();
    private readonly Mock<IRepository<GenerationLog>> _logRepoMock = new();
    private readonly Mock<IStorageService> _storageMock = new();

    private DownloadDocumentVersionUseCase CreateSut() =>
        new(_documentRepoMock.Object, _versionRepoMock.Object, _logRepoMock.Object, _storageMock.Object);

    [Fact]
    public async Task ExecuteAsync_WhenVersionExists_ReturnsStreamAndMetadata()
    {
        // Arrange
        var documentId = Guid.NewGuid();
        var logId = Guid.NewGuid();
        var document = DocumentTestFactory.Create(documentId, "SC-001", Guid.NewGuid());
        var docVersion = DocumentVersionTestFactory.Create(documentId: documentId, version: 1, templateVersionId: Guid.NewGuid(), generationLogId: logId);
        var log = GenerationLogTestFactory.Create(id: logId, outputKey: "outputs/sc001_v1.pdf", outputFormat: OutputFormat.Pdf, durationMs: 1);
        var pdfBytes = new byte[] { 0x25, 0x50, 0x44, 0x46 };

        _documentRepoMock
            .Setup(r => r.FirstOrDefaultAsync(It.IsAny<Expression<Func<Document, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(document);
        _versionRepoMock
            .Setup(r => r.FirstOrDefaultAsync(It.IsAny<Expression<Func<DocumentVersion, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(docVersion);
        _logRepoMock
            .Setup(r => r.GetByIdAsync(logId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(log);
        _storageMock
            .Setup(s => s.DownloadAsync("outputs", "outputs/sc001_v1.pdf", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MemoryStream(pdfBytes));

        // Act
        var result = await CreateSut().ExecuteAsync(new DownloadDocumentVersionQuery("SC-001", 1));

        // Assert
        result.ContentType.Should().Be("application/pdf");
        result.FileName.Should().Be("SC-001_v1.pdf");
        result.Stream.Should().NotBeNull();
    }

    [Fact]
    public async Task ExecuteAsync_WhenDocumentDoesNotExist_ThrowsNotFoundException()
    {
        // Arrange
        _documentRepoMock
            .Setup(r => r.FirstOrDefaultAsync(It.IsAny<Expression<Func<Document, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Document?)null);

        // Act
        var act = () => CreateSut().ExecuteAsync(new DownloadDocumentVersionQuery("MISSING", 1));

        // Assert
        await act.Should().ThrowAsync<NotFoundException>();
    }
}
