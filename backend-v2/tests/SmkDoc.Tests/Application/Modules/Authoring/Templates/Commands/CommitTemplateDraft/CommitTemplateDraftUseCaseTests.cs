using System.Text;
using SmkDoc.Application.Common;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.Authoring.FieldMappings.DTOs;
using SmkDoc.Application.Modules.Authoring.Templates.Commands.CommitTemplateDraft;
using SmkDoc.Application.Modules.Authoring.Templates.DTOs;
using SmkDoc.Domain.Entities;
using SmkDoc.Tests.Common.Fixtures;

namespace SmkDoc.Tests.Application.Modules.Authoring.Templates.Commands.CommitTemplateDraft;

public class CommitTemplateDraftUseCaseTests
{
    private readonly TemplateAuthoringTestFixture _fixture = new();

    private CommitTemplateDraftUseCase CreateSut() => _fixture.BuildCommitTemplateDraftUseCase();

    [Fact]
    public async Task ExecuteAsync_WhenValidDraft_ShouldUploadToMinioAndPersistToDb()
    {
        // Arrange
        var fileBytes = Encoding.UTF8.GetBytes("xlsx-content");
        var entry = new TemplateDraftEntry(fileBytes, "report.xlsx", ".xlsx", []);
        var request = new CommitDraftCommand("Report", "report", "finance", [], ProjectId: Guid.NewGuid());

        _fixture.DraftCache.Setup(c => c.GetAsync("draft-commit", It.IsAny<CancellationToken>())).ReturnsAsync(entry);
        _fixture.Storage.Setup(s => s.UploadAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Stream>(),
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("storage-key");

        // Act
        var templateId = await CreateSut().ExecuteAsync(new CommitTemplateDraftCommand("draft-commit", request), CancellationToken.None);

        // Assert
        templateId.Should().NotBeEmpty();
        _fixture.Storage.Verify(s => s.UploadAsync(StorageBuckets.Templates, It.Is<string>(k => k.StartsWith($"templates/{request.ProjectId}/")), It.IsAny<Stream>(),
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", It.IsAny<CancellationToken>()),
            Times.Once);
        _fixture.Uow.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
        _fixture.DraftCache.Verify(c => c.RemoveAsync("draft-commit", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_WhenDbWriteFails_ShouldRollbackMinioUpload()
    {
        // Arrange
        var entry = new TemplateDraftEntry(Encoding.UTF8.GetBytes("data"), "t.docx", ".docx", []);
        var request = new CommitDraftCommand("T", "test-template", null, [], ProjectId: Guid.NewGuid());

        _fixture.DraftCache.Setup(c => c.GetAsync("draft-fail", It.IsAny<CancellationToken>())).ReturnsAsync(entry);
        _fixture.Storage.Setup(s => s.UploadAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Stream>(),
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("storage-key");
        _fixture.Uow.Setup(u => u.CommitAsync(It.IsAny<CancellationToken>())).ThrowsAsync(new Exception("DB error"));

        // Act & Assert
        await CreateSut().Invoking(u => u.ExecuteAsync(new CommitTemplateDraftCommand("draft-fail", request), CancellationToken.None))
            .Should().ThrowAsync<Exception>().WithMessage("DB error");

        _fixture.Storage.Verify(s => s.DeleteAsync(StorageBuckets.Templates, It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_WhenDraftNotFound_ShouldThrowDraftExpiredException()
    {
        // Arrange
        _fixture.DraftCache.Setup(c => c.GetAsync("stale", It.IsAny<CancellationToken>())).ReturnsAsync((TemplateDraftEntry?)null);
        var request = new CommitDraftCommand("T", "test-template", null, [], ProjectId: Guid.NewGuid());

        // Act & Assert
        await CreateSut().Invoking(u => u.ExecuteAsync(new CommitTemplateDraftCommand("stale", request), CancellationToken.None))
            .Should().ThrowAsync<DraftExpiredException>();
    }

    [Fact]
    public async Task ExecuteAsync_WhenMappingsProvided_ShouldPersistAllFieldMappings()
    {
        // Arrange
        var entry = new TemplateDraftEntry(Encoding.UTF8.GetBytes("html"), "t.html", ".html", []);
        var mappings = new List<SaveFieldMappingItemDto>
        {
            new("name", "customer.name", "ชื่อลูกค้า", true, null, null, 0),
            new("total", "invoice.total", "ยอดรวม", false, null, null, 1),
        };
        var request = new CommitDraftCommand("Invoice", "invoice", null, mappings, ProjectId: Guid.NewGuid());

        _fixture.DraftCache.Setup(c => c.GetAsync("draft-map", It.IsAny<CancellationToken>())).ReturnsAsync(entry);
        _fixture.Storage.Setup(s => s.UploadAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Stream>(),
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("key");

        // Act
        await CreateSut().ExecuteAsync(new CommitTemplateDraftCommand("draft-map", request), CancellationToken.None);

        // Assert
        _fixture.TemplateRepo.Verify(r => r.AddAsync(
            It.Is<Template>(t => t.FieldMappings.Count == 2),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_WhenMissingProjectIdAndNoContext_ShouldThrowValidationExceptionWithoutUploading()
    {
        // Arrange
        var entry = new TemplateDraftEntry(Encoding.UTF8.GetBytes("data"), "report.docx", ".docx", []);
        var request = new CommitDraftCommand("Report", "report", null, [], ProjectId: null);

        _fixture.DraftCache.Setup(c => c.GetAsync("draft-no-proj", It.IsAny<CancellationToken>())).ReturnsAsync(entry);

        // Act & Assert
        var act = () => CreateSut().ExecuteAsync(new CommitTemplateDraftCommand("draft-no-proj", request), CancellationToken.None);
        var ex = await act.Should().ThrowAsync<ValidationException>();
        ex.Which.Errors.Should().ContainKey("ProjectId");

        _fixture.Storage.Verify(s => s.UploadAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_WhenNullProjectIdInRequest_ShouldFallbackToCurrentProjectContext()
    {
        // Arrange
        var contextProjectId = Guid.NewGuid();
        _fixture.ProjectContext.Setup(c => c.IsResolved).Returns(true);
        _fixture.ProjectContext.Setup(c => c.ProjectId).Returns(new SmkDoc.Domain.ValueObjects.Ids.ProjectId(contextProjectId));

        var fileBytes = Encoding.UTF8.GetBytes("docx-content");
        var entry = new TemplateDraftEntry(fileBytes, "report.docx", ".docx", []);
        var request = new CommitDraftCommand("Report", "report", null, [], ProjectId: null);

        _fixture.DraftCache.Setup(c => c.GetAsync("draft-ctx", It.IsAny<CancellationToken>())).ReturnsAsync(entry);
        _fixture.Storage.Setup(s => s.UploadAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Stream>(),
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("storage-key");

        // Act
        var result = await CreateSut().ExecuteAsync(new CommitTemplateDraftCommand("draft-ctx", request), CancellationToken.None);

        // Assert
        result.Should().NotBeEmpty();
        _fixture.Storage.Verify(s => s.UploadAsync(StorageBuckets.Templates, It.Is<string>(k => k.StartsWith($"templates/{contextProjectId}/")), It.IsAny<Stream>(),
                "application/vnd.openxmlformats-officedocument.wordprocessingml.document", It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
