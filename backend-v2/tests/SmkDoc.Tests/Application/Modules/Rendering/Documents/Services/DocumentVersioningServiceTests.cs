using System.Linq.Expressions;
using FluentAssertions;
using Moq;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.Rendering.Documents.Services;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Interfaces;
using Xunit;

namespace SmkDoc.Tests.Application.Modules.Rendering.Documents.Services;

public class DocumentVersioningServiceTests
{
    private readonly Mock<IRepository<Document>> _mockDocumentRepo = new();
    private readonly Mock<IRepository<DocumentVersion>> _mockDocVersionRepo = new();
    private readonly Mock<IExecutionContext> _mockContext = new();
    private readonly Mock<IUnitOfWork> _mockUow = new();

    private DocumentVersioningService BuildService() => new(
        _mockDocumentRepo.Object, _mockDocVersionRepo.Object, _mockContext.Object, _mockUow.Object);

    [Fact]
    public async Task RecordVersionAsync_WhenNewDocument_ShouldCreateDocumentAndFirstVersion()
    {
        // Arrange
        var service = BuildService();
        var templateId = Guid.NewGuid();
        var versionId = Guid.NewGuid();
        var genId = Guid.NewGuid();

        _mockDocumentRepo.Setup(r => r.FirstOrDefaultAsync(It.IsAny<Expression<Func<Document, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Document?)null);

        _mockDocVersionRepo.Setup(r => r.MaxOrDefaultAsync(It.IsAny<Expression<Func<DocumentVersion, bool>>>(), It.IsAny<Expression<Func<DocumentVersion, int>>>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        // Act
        await service.RecordVersionAsync("INV-001", templateId, versionId, genId, "Initial version");

        // Assert
        _mockDocumentRepo.Verify(r => r.AddAsync(
            It.Is<Document>(d => d.DocumentRef == "INV-001" && d.TemplateId == templateId),
            It.IsAny<CancellationToken>()), Times.Once);

        _mockDocVersionRepo.Verify(r => r.AddAsync(
            It.Is<DocumentVersion>(v => v.Version == 1 && v.GenerationLogId == genId),
            It.IsAny<CancellationToken>()), Times.Once);

        _mockUow.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
