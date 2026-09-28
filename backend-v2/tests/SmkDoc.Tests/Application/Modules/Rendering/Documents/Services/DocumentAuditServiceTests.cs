using FluentAssertions;
using Moq;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.Rendering.Documents.Services;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Enums;
using SmkDoc.Domain.Interfaces;
using Xunit;

namespace SmkDoc.Tests.Application.Modules.Rendering.Documents.Services;

public class DocumentAuditServiceTests
{
    private readonly Mock<IRepository<GenerationLog>> _mockLogRepo = new();
    private readonly Mock<IExecutionContext> _mockContext = new();
    private readonly Mock<IUnitOfWork> _mockUow = new();

    private DocumentAuditService BuildService() => new(
        _mockLogRepo.Object, _mockContext.Object, _mockUow.Object);

    [Fact]
    public async Task LogValidationFailureAsync_ShouldAddFailedLogAndCommitImmediately()
    {
        // Arrange
        var service = BuildService();
        _mockContext.Setup(c => c.CallerApp).Returns("crm-app");

        // Act
        await service.LogValidationFailureAsync(
            Guid.NewGuid(), Guid.NewGuid(), "{}", OutputFormat.Pdf, 50, "Schema violation");

        // Assert
        _mockLogRepo.Verify(r => r.AddAsync(
            It.Is<GenerationLog>(l => l.Status == "VALIDATION_FAILED" && l.CallerApp == "crm-app"),
            It.IsAny<CancellationToken>()), Times.Once);
        _mockUow.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task LogSuccessAsync_ShouldAddSuccessLogWithoutPrematureCommit()
    {
        // Arrange
        var service = BuildService();
        _mockContext.Setup(c => c.CallerApp).Returns("pos-app");
        var genId = Guid.NewGuid();

        // Act
        await service.LogSuccessAsync(
            genId, Guid.NewGuid(), Guid.NewGuid(), "{}", "outputs/receipt.pdf", OutputFormat.Pdf, 1024, 150);

        // Assert
        _mockLogRepo.Verify(r => r.AddAsync(
            It.Is<GenerationLog>(l => l.Id == genId && l.Status == "SUCCESS" && l.OutputKey == "outputs/receipt.pdf"),
            It.IsAny<CancellationToken>()), Times.Once);
        _mockUow.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
