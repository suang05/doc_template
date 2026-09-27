using System.Linq.Expressions;
using FluentAssertions;
using Moq;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.UseCases.Documents;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Enums;
using SmkDoc.Domain.Exceptions;
using Xunit;

namespace SmkDoc.Tests.Application.UseCases.Documents;

public class DocumentVersionUseCaseTests
{
    private readonly Mock<IRepository<Document>>        _mockDocumentRepo = new();
    private readonly Mock<IRepository<DocumentVersion>> _mockVersionRepo  = new();
    private readonly Mock<IRepository<GenerationLog>>   _mockLogRepo      = new();
    private readonly Mock<IStorageService>              _mockStorage      = new();

    private DocumentVersionUseCase BuildUseCase() => new(
        _mockDocumentRepo.Object,
        _mockVersionRepo.Object,
        _mockLogRepo.Object,
        _mockStorage.Object
    );

    [Fact]
    public async Task GetVersionsByRefAsync_ShouldReturnOrderedVersions()
    {
        // Arrange
        var documentId = Guid.NewGuid();
        var document = new Document("SC-001", Guid.NewGuid()) { Id = documentId };

        _mockDocumentRepo.Setup(r => r.FirstOrDefaultAsync(It.IsAny<Expression<Func<Document, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(document);

        _mockVersionRepo.Setup(r => r.ListAsync(It.IsAny<Expression<Func<DocumentVersion, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                new DocumentVersion(documentId, 1, Guid.NewGuid(), null, null, null) { Id = Guid.NewGuid() },
                new DocumentVersion(documentId, 2, Guid.NewGuid(), null, null, null) { Id = Guid.NewGuid() }
            ]);

        // Act
        var result = await BuildUseCase().GetVersionsByRefAsync("SC-001");

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

        // Act
        var result = await BuildUseCase().GetVersionsByRefAsync("DOES-NOT-EXIST");

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task DownloadVersionAsync_ShouldReturnStreamFromGenerationLog()
    {
        // Arrange
        var documentId = Guid.NewGuid();
        var logId      = Guid.NewGuid();
        var document   = new Document("SC-001", Guid.NewGuid()) { Id = documentId };
        var docVersion = new DocumentVersion(documentId, 1, Guid.NewGuid(), logId, null, null) { Id = Guid.NewGuid() };
        var log        = new GenerationLog(null, null, null, null, null, null, "outputs/sc001_v1.pdf", OutputFormat.Pdf, null, null, null, 1, "SUCCESS", null) { Id = logId };
        var pdfBytes   = new byte[] { 0x25, 0x50, 0x44, 0x46 };

        _mockDocumentRepo.Setup(r => r.FirstOrDefaultAsync(It.IsAny<Expression<Func<Document, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(document);
        _mockVersionRepo.Setup(r => r.FirstOrDefaultAsync(It.IsAny<Expression<Func<DocumentVersion, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(docVersion);
        _mockLogRepo.Setup(r => r.GetByIdAsync(logId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(log);
        _mockStorage.Setup(s => s.DownloadAsync("outputs", "outputs/sc001_v1.pdf", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MemoryStream(pdfBytes));

        // Act
        var (stream, contentType, fileName) = await BuildUseCase().DownloadVersionAsync("SC-001", 1);

        // Assert
        contentType.Should().Be("application/pdf");
        fileName.Should().Be("SC-001_v1.pdf");
        stream.Should().NotBeNull();
    }

    [Fact]
    public async Task DownloadVersionAsync_WhenDocumentNotFound_ShouldThrowNotFoundException()
    {
        _mockDocumentRepo.Setup(r => r.FirstOrDefaultAsync(It.IsAny<Expression<Func<Document, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Document?)null);

        var act = () => BuildUseCase().DownloadVersionAsync("MISSING", 1);
        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task GetDownloadUrlByLogIdAsync_ShouldReturnPresignedUrl()
    {
        // Arrange
        var logId = Guid.NewGuid();
        var log = new GenerationLog(null, null, null, null, null, null, "outputs/doc.pdf", null, null, null, null, 1, "SUCCESS", null) { Id = logId };

        _mockLogRepo.Setup(r => r.GetByIdAsync(logId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(log);
        _mockStorage.Setup(s => s.GetPresignedUrlAsync("outputs", "outputs/doc.pdf", It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("https://minio.sammakorn.co.th/outputs/doc.pdf?token=abc");

        // Act
        var url = await BuildUseCase().GetDownloadUrlByLogIdAsync(logId);

        // Assert
        url.Should().Contain("minio.sammakorn.co.th");
    }
}
