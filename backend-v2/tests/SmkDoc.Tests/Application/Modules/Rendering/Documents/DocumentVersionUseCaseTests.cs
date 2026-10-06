using System.Linq.Expressions;
using FluentAssertions;
using Moq;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.Rendering.Documents.Queries.DownloadDocumentVersion;
using SmkDoc.Application.Modules.Rendering.Documents.Queries.GetDocumentVersions;
using SmkDoc.Application.Modules.Rendering.Logs.Queries.GetLogDownloadUrl;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Enums;
using SmkDoc.Domain.Exceptions;
using SmkDoc.Tests.Common.Factories;
using Xunit;

namespace SmkDoc.Tests.Application.Modules.Rendering.Documents;

public class DocumentVersionUseCaseTests
{
    private readonly Mock<IRepository<Document>>        _mockDocumentRepo = new();
    private readonly Mock<IRepository<DocumentVersion>> _mockVersionRepo  = new();
    private readonly Mock<IRepository<GenerationLog>>   _mockLogRepo      = new();
    private readonly Mock<IStorageService>              _mockStorage      = new();

    [Fact]
    public async Task GetVersionsByRefAsync_ShouldReturnOrderedVersions()
    {
        // Arrange
        var documentId = Guid.NewGuid();
        var document = DocumentTestFactory.Create(documentId, "SC-001", Guid.NewGuid());

        _mockDocumentRepo.Setup(r => r.FirstOrDefaultAsync(It.IsAny<Expression<Func<Document, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(document);

        _mockVersionRepo.Setup(r => r.ListAsync(It.IsAny<Expression<Func<DocumentVersion, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                DocumentVersionTestFactory.Create(documentId: documentId, version: 1),
                DocumentVersionTestFactory.Create(documentId: documentId, version: 2)
            ]);

        var useCase = new GetDocumentVersionsUseCase(_mockDocumentRepo.Object, _mockVersionRepo.Object);

        // Act
        var result = await useCase.ExecuteAsync(new GetDocumentVersionsQuery("SC-001"));

        // Assert
        result.Should().HaveCount(2);
        result[0].Version.Should().Be(2); // descending order
        result[1].Version.Should().Be(1);
        result[0].DocumentRef.Should().Be("SC-001");
    }

    [Fact]
    public async Task GetVersionsByRefAsync_WhenDocumentNotFound_ShouldReturnEmptyList()
    {
        // Arrange
        _mockDocumentRepo.Setup(r => r.FirstOrDefaultAsync(It.IsAny<Expression<Func<Document, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Document?)null);

        var useCase = new GetDocumentVersionsUseCase(_mockDocumentRepo.Object, _mockVersionRepo.Object);

        // Act
        var result = await useCase.ExecuteAsync(new GetDocumentVersionsQuery("DOES-NOT-EXIST"));

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task DownloadVersionAsync_ShouldReturnStreamFromGenerationLog()
    {
        // Arrange
        var documentId = Guid.NewGuid();
        var logId      = Guid.NewGuid();
        var document   = DocumentTestFactory.Create(documentId, "SC-001", Guid.NewGuid());
        var docVersion = DocumentVersionTestFactory.Create(documentId: documentId, version: 1, templateVersionId: Guid.NewGuid(), generationLogId: logId);
        var log        = GenerationLogTestFactory.Create(id: logId, outputKey: "outputs/sc001_v1.pdf", outputFormat: OutputFormat.Pdf, durationMs: 1);
        var pdfBytes   = new byte[] { 0x25, 0x50, 0x44, 0x46 };

        _mockDocumentRepo.Setup(r => r.FirstOrDefaultAsync(It.IsAny<Expression<Func<Document, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(document);
        _mockVersionRepo.Setup(r => r.FirstOrDefaultAsync(It.IsAny<Expression<Func<DocumentVersion, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(docVersion);
        _mockLogRepo.Setup(r => r.GetByIdAsync(logId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(log);
        _mockStorage.Setup(s => s.DownloadAsync("outputs", "outputs/sc001_v1.pdf", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MemoryStream(pdfBytes));

        var useCase = new DownloadDocumentVersionUseCase(_mockDocumentRepo.Object, _mockVersionRepo.Object, _mockLogRepo.Object, _mockStorage.Object);

        // Act
        var result = await useCase.ExecuteAsync(new DownloadDocumentVersionQuery("SC-001", 1));

        // Assert
        result.ContentType.Should().Be("application/pdf");
        result.FileName.Should().Be("SC-001_v1.pdf");
        result.Stream.Should().NotBeNull();
    }

    [Fact]
    public async Task DownloadVersionAsync_WhenDocumentNotFound_ShouldThrowNotFoundException()
    {
        _mockDocumentRepo.Setup(r => r.FirstOrDefaultAsync(It.IsAny<Expression<Func<Document, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Document?)null);

        var useCase = new DownloadDocumentVersionUseCase(_mockDocumentRepo.Object, _mockVersionRepo.Object, _mockLogRepo.Object, _mockStorage.Object);

        Func<Task> act = () => useCase.ExecuteAsync(new DownloadDocumentVersionQuery("MISSING", 1));
        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task GetDownloadUrlByLogIdAsync_ShouldReturnPresignedUrl()
    {
        // Arrange
        var logId = Guid.NewGuid();
        var log = GenerationLogTestFactory.Create(id: logId, outputKey: "outputs/doc.pdf", durationMs: 1);

        _mockLogRepo.Setup(r => r.GetByIdAsync(logId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(log);
        _mockStorage.Setup(s => s.GetPresignedUrlAsync("outputs", "outputs/doc.pdf", It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("https://minio.sammakorn.co.th/outputs/doc.pdf?token=abc");

        var useCase = new GetLogDownloadUrlUseCase(_mockLogRepo.Object, _mockStorage.Object);

        // Act
        var url = await useCase.ExecuteAsync(new GetLogDownloadUrlQuery(logId));

        // Assert
        url.Should().Contain("minio.sammakorn.co.th");
    }
}
