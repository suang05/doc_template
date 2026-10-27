using SmkDoc.Application.Modules.Rendering.Documents.Queries.GetDocumentVersions;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Interfaces;
using SmkDoc.Domain.ValueObjects;
using SmkDoc.Tests.Common.Builders;

namespace SmkDoc.Tests.Application.Modules.Rendering.Documents.Queries.GetDocumentVersions;

public sealed class GetDocumentVersionsUseCaseTests
{
    private readonly IDocumentRepository _documentRepo = Substitute.For<IDocumentRepository>();

    private GetDocumentVersionsUseCase CreateSut() =>
        new(_documentRepo);

    [Fact]
    public async Task ExecuteAsync_WhenDocumentExists_ReturnsVersionsInDescendingOrder()
    {
        // Arrange
        var documentId = Guid.NewGuid();
        var document = new DocumentBuilder().WithId(documentId).WithDocumentRef("SC-001").WithTemplateId(Guid.NewGuid()).Build();

        _documentRepo.GetByDocumentRefAsync(Arg.Any<DocumentReference>(), Arg.Any<CancellationToken>())
            .Returns(document);

        _documentRepo.GetVersionHistoryAsync(Arg.Any<DocumentReference>(), Arg.Any<CancellationToken>())
            .Returns([
                new DocumentVersionBuilder().ForDocument(documentId).WithVersion(2).Build(),
                new DocumentVersionBuilder().ForDocument(documentId).WithVersion(1).Build()
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
        _documentRepo.GetByDocumentRefAsync(Arg.Any<DocumentReference>(), Arg.Any<CancellationToken>())
            .Returns((Document?)null);

        // Act
        var result = await CreateSut().ExecuteAsync(new GetDocumentVersionsQuery("DOES-NOT-EXIST"));

        // Assert
        result.Should().BeEmpty();
    }
}
