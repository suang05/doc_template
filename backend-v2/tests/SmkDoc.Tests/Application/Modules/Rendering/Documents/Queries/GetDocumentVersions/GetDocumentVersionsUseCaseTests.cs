using System.Linq.Expressions;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.Rendering.Documents.Queries.GetDocumentVersions;
using SmkDoc.Domain.Entities;
using SmkDoc.Tests.Common.Factories;

namespace SmkDoc.Tests.Application.Modules.Rendering.Documents.Queries.GetDocumentVersions;

public class GetDocumentVersionsUseCaseTests
{
    private readonly Mock<IRepository<Document>> _documentRepoMock = new();
    private readonly Mock<IRepository<DocumentVersion>> _versionRepoMock = new();

    private GetDocumentVersionsUseCase CreateSut() =>
        new(_documentRepoMock.Object, _versionRepoMock.Object);

    [Fact]
    public async Task ExecuteAsync_WhenDocumentExists_ReturnsVersionsInDescendingOrder()
    {
        // Arrange
        var documentId = Guid.NewGuid();
        var document = DocumentTestFactory.Create(documentId, "SC-001", Guid.NewGuid());

        _documentRepoMock
            .Setup(r => r.FirstOrDefaultAsync(It.IsAny<Expression<Func<Document, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(document);

        _versionRepoMock
            .Setup(r => r.ListAsync(It.IsAny<Expression<Func<DocumentVersion, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                DocumentVersionTestFactory.Create(documentId: documentId, version: 1),
                DocumentVersionTestFactory.Create(documentId: documentId, version: 2)
            ]);

        var sut = CreateSut();

        // Act
        var result = await sut.ExecuteAsync(new GetDocumentVersionsQuery("SC-001"));

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
        _documentRepoMock
            .Setup(r => r.FirstOrDefaultAsync(It.IsAny<Expression<Func<Document, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Document?)null);

        var sut = CreateSut();

        // Act
        var result = await sut.ExecuteAsync(new GetDocumentVersionsQuery("DOES-NOT-EXIST"));

        // Assert
        result.Should().BeEmpty();
    }
}
