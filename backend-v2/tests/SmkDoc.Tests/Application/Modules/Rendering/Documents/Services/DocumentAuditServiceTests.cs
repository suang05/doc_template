using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.Rendering.Documents.Services;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Enums;
using SmkDoc.Domain.Interfaces;

namespace SmkDoc.Tests.Application.Modules.Rendering.Documents.Services;

public sealed class DocumentAuditServiceTests
{
    private readonly IRepository<GenerationLog> _logRepo = Substitute.For<IRepository<GenerationLog>>();
    private readonly IExecutionContext _context = Substitute.For<IExecutionContext>();
    private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();

    private DocumentAuditService CreateSut() => new(
        _logRepo, _context, _uow);

    [Fact]
    public async Task LogValidationFailureAsync_ShouldAddFailedLogAndCommitImmediately()
    {
        // Arrange
        _context.CallerApp.Returns("crm-app");
        _context.ProjectId.Returns(Guid.NewGuid());

        // Act
        await CreateSut().LogValidationFailureAsync(
            Guid.NewGuid(), Guid.NewGuid(), "{}", OutputFormat.Pdf, 50, "Schema violation");

        // Assert
        await _logRepo.Received(1).AddAsync(
            Arg.Is<GenerationLog>(l => l.Status == "VALIDATION_FAILED" && l.CallerApp == "crm-app"),
            Arg.Any<CancellationToken>());
        await _uow.Received(1).CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task LogSuccessAsync_ShouldAddSuccessLogWithoutPrematureCommit()
    {
        // Arrange
        _context.CallerApp.Returns("pos-app");
        _context.ProjectId.Returns(Guid.NewGuid());
        var genId = Guid.NewGuid();

        // Act
        await CreateSut().LogSuccessAsync(
            genId, Guid.NewGuid(), Guid.NewGuid(), "{}", "outputs/receipt.pdf", OutputFormat.Pdf, 1024, 150);

        // Assert
        await _logRepo.Received(1).AddAsync(
            Arg.Is<GenerationLog>(l => l.Id == genId && l.Status == "SUCCESS" && l.OutputKey == "outputs/receipt.pdf"),
            Arg.Any<CancellationToken>());
        await _uow.DidNotReceive().CommitAsync(Arg.Any<CancellationToken>());
    }
}
