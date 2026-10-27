using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.Rendering.Documents.Services;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Interfaces;
using SmkDoc.Domain.ValueObjects;
using SmkDoc.Domain.ValueObjects.Ids;

namespace SmkDoc.Tests.Application.Modules.Rendering.Documents.Services;

public sealed class DocumentVersioningServiceTests
{
    private readonly IDocumentRepository _documentRepo = Substitute.For<IDocumentRepository>();
    private readonly IExecutionContext _context = Substitute.For<IExecutionContext>();
    private readonly ICurrentProjectContext _projectContext = Substitute.For<ICurrentProjectContext>();

    private DocumentVersioningService CreateSut() => new(
        _documentRepo, _context, _projectContext);

    [Fact]
    public async Task RecordVersionAsync_WhenNewDocument_ShouldCreateDocumentAndFirstVersion()
    {
        // Arrange
        var templateId = Guid.NewGuid();
        var versionId = Guid.NewGuid();
        var genId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        _context.ProjectId.Returns(projectId);
        _projectContext.IsResolved.Returns(true);
        _projectContext.ProjectId.Returns(new ProjectId(projectId));

        _documentRepo.GetByDocumentRefWithVersionsAsync(Arg.Any<DocumentReference>(), Arg.Any<CancellationToken>())
            .Returns((Document?)null);

        // Act
        await CreateSut().RecordVersionAsync("INV-001", templateId, versionId, genId, "Initial version");

        // Assert
        await _documentRepo.Received(1).AddAsync(
            Arg.Is<Document>(d => d.DocumentRef == "INV-001" && d.TemplateId == new TemplateId(templateId) && d.Versions.Count == 1 && d.Versions.First().Version == 1),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RecordVersionAsync_WhenExistingDocument_ShouldIncrementVersionOnAggregate()
    {
        // Arrange
        var templateId = Guid.NewGuid();
        var versionId = Guid.NewGuid();
        var genId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        _context.ProjectId.Returns(projectId);
        _projectContext.IsResolved.Returns(true);
        _projectContext.ProjectId.Returns(new ProjectId(projectId));

        var existingDoc = Document.Create(
            DocumentReference.Create("INV-001"),
            new TemplateId(templateId),
            DateTimeOffset.UtcNow,
            new ProjectId(projectId));
        existingDoc.CreateVersion(versionId, Guid.NewGuid(), "v1", "app", DateTimeOffset.UtcNow);

        _documentRepo.GetByDocumentRefWithVersionsAsync(Arg.Any<DocumentReference>(), Arg.Any<CancellationToken>())
            .Returns(existingDoc);

        // Act
        await CreateSut().RecordVersionAsync("INV-001", templateId, versionId, genId, "Second version");

        // Assert
        existingDoc.Versions.Should().HaveCount(2);
        existingDoc.Versions.Last().Version.Should().Be(2);
        existingDoc.Versions.Last().GenerationLogId.Should().Be(genId);
        await _documentRepo.DidNotReceive().AddAsync(Arg.Any<Document>(), Arg.Any<CancellationToken>());
    }
}
