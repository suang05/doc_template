using System.Linq.Expressions;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.Rendering.Documents.Queries.GetDocumentVersions;
using SmkDoc.Domain.Entities;
using SmkDoc.Tests.Common.Builders;

namespace SmkDoc.Tests.Application.Modules.Rendering.Documents.Queries.GetDocumentVersions;

public sealed class GetDocumentVersionsUseCaseTests
{
    private readonly IRepository<Document> _documentRepo = Substitute.For<IRepository<Document>>();
    private readonly IRepository<DocumentVersion> _versionRepo = Substitute.For<IRepository<DocumentVersion>>();

    private GetDocumentVersionsUseCase CreateSut() =>
        new(_documentRepo, _versionRepo);

    [Fact]
    public async Task ExecuteAsync_WhenDocumentExists_ReturnsVersionsInDescendingOrder()
    {
        // Arrange
        var documentId = Guid.NewGuid();
        var document = new DocumentBuilder().WithId(documentId).WithDocumentRef("SC-001").WithTemplateId(Guid.NewGuid()).Build();

        _documentRepo.FirstOrDefaultAsync(Arg.Any<Expression<Func<Document, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(document);

        _versionRepo.ListAsync(Arg.Any<Expression<Func<DocumentVersion, bool>>>(), Arg.Any<CancellationToken>())
            .Returns([
                new DocumentVersionBuilder().ForDocument(documentId).WithVersion(1).Build(),
                new DocumentVersionBuilder().ForDocument(documentId).WithVersion(2).Build()
            ]);

        // Act
        var result = await CreateSut().ExecuteAsync(new GetDocumentVersionsQuery("SC-001"));

        // Assert
        result.Should().HaveCount(2);
        result[0].Version.Should().Be(2);
        result[1].Version.Should().Be(1);
        result[0].DocumentRef.Should().Be("SC-001");
    }

    [Fact]
    public async Task ExecuteAsync_WhenDocumentDoesNotExist_ReturnsEmptyList()
    {
        // Arrange
        _documentRepo.FirstOrDefaultAsync(Arg.Any<Expression<Func<Document, bool>>>(), Arg.Any<CancellationToken>())
            .Returns((Document?)null);

        // Act
        var result = await CreateSut().ExecuteAsync(new GetDocumentVersionsQuery("DOES-NOT-EXIST"));

        // Assert
        result.Should().BeEmpty();
    }
}
