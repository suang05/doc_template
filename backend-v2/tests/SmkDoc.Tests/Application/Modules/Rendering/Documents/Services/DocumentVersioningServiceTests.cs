using System.Linq.Expressions;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.Rendering.Documents.Services;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Interfaces;

namespace SmkDoc.Tests.Application.Modules.Rendering.Documents.Services;

public sealed class DocumentVersioningServiceTests
{
    private readonly IRepository<Document> _documentRepo = Substitute.For<IRepository<Document>>();
    private readonly IRepository<DocumentVersion> _docVersionRepo = Substitute.For<IRepository<DocumentVersion>>();
    private readonly IExecutionContext _context = Substitute.For<IExecutionContext>();

    private DocumentVersioningService CreateSut() => new(
        _documentRepo, _docVersionRepo, _context);

    [Fact]
    public async Task RecordVersionAsync_WhenNewDocument_ShouldCreateDocumentAndFirstVersion()
    {
        // Arrange
        var templateId = Guid.NewGuid();
        var versionId = Guid.NewGuid();
        var genId = Guid.NewGuid();

        _documentRepo.FirstOrDefaultAsync(Arg.Any<Expression<Func<Document, bool>>>(), Arg.Any<CancellationToken>())
            .Returns((Document?)null);

        _docVersionRepo.MaxOrDefaultAsync(Arg.Any<Expression<Func<DocumentVersion, bool>>>(), Arg.Any<Expression<Func<DocumentVersion, int>>>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(0);

        // Act
        await CreateSut().RecordVersionAsync("INV-001", templateId, versionId, genId, "Initial version");

        // Assert
        await _documentRepo.Received(1).AddAsync(
            Arg.Is<Document>(d => d.DocumentRef == "INV-001" && d.TemplateId == templateId),
            Arg.Any<CancellationToken>());

        await _docVersionRepo.Received(1).AddAsync(
            Arg.Is<DocumentVersion>(v => v.Version == 1 && v.GenerationLogId == genId),
            Arg.Any<CancellationToken>());
    }
}
