using System.Linq.Expressions;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.Rendering.Documents.Queries.DownloadDocumentVersion;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Exceptions;
using SmkDoc.Tests.Common.Builders;

namespace SmkDoc.Tests.Application.Modules.Rendering.Documents.Queries.DownloadDocumentVersion;

public sealed class DownloadDocumentVersionUseCaseTests
{
    private readonly IRepository<Document> _documentRepo = Substitute.For<IRepository<Document>>();
    private readonly IRepository<DocumentVersion> _versionRepo = Substitute.For<IRepository<DocumentVersion>>();
    private readonly IRepository<GenerationLog> _logRepo = Substitute.For<IRepository<GenerationLog>>();
    private readonly IStorageService _storage = Substitute.For<IStorageService>();

    private DownloadDocumentVersionUseCase CreateSut() =>
        new(_documentRepo, _versionRepo, _logRepo, _storage);

    [Fact]
    public async Task ExecuteAsync_WhenVersionExists_ReturnsStreamAndMetadata()
    {
        // Arrange
        var documentId = Guid.NewGuid();
        var logId = Guid.NewGuid();
        var document = new DocumentBuilder().WithId(documentId).WithDocumentRef("SC-001").WithTemplateId(Guid.NewGuid()).Build();
        var docVersion = new DocumentVersionBuilder().ForDocument(documentId).WithVersion(1).WithTemplateVersionId(Guid.NewGuid()).WithGenerationLogId(logId).Build();
        var log = GenerationLogBuilder.Success(id: logId, outputKey: "outputs/sc001_v1.pdf", durationMs: 1);
        var pdfBytes = new byte[] { 0x25, 0x50, 0x44, 0x46 };

        _documentRepo.FirstOrDefaultAsync(Arg.Any<Expression<Func<Document, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(document);
        _versionRepo.FirstOrDefaultAsync(Arg.Any<Expression<Func<DocumentVersion, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(docVersion);
        _logRepo.GetByIdAsync(logId, Arg.Any<CancellationToken>())
            .Returns(log);
        _storage.DownloadAsync("outputs", "outputs/sc001_v1.pdf", Arg.Any<CancellationToken>())
            .Returns(new MemoryStream(pdfBytes));

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
        _documentRepo.FirstOrDefaultAsync(Arg.Any<Expression<Func<Document, bool>>>(), Arg.Any<CancellationToken>())
            .Returns((Document?)null);

        // Act
        var act = () => CreateSut().ExecuteAsync(new DownloadDocumentVersionQuery("MISSING", 1));

        // Assert
        await act.Should().ThrowAsync<NotFoundException>();
    }
}
