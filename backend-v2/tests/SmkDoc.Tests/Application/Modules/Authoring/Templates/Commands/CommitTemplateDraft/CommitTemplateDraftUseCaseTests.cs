using System.Text;
using FluentAssertions;
using Moq;
using SmkDoc.Application.Common;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.Authoring.FieldMappings.DTOs;
using SmkDoc.Application.Modules.Authoring.Templates.Commands.CommitTemplateDraft;
using SmkDoc.Application.Modules.Authoring.Templates.DTOs;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Exceptions;
using SmkDoc.Domain.Interfaces;
using SmkDoc.Domain.ValueObjects;
using Xunit;

namespace SmkDoc.Tests.Application.Modules.Authoring.Templates.Commands.CommitTemplateDraft;

public class CommitTemplateDraftUseCaseTests
{
    private readonly Mock<ITemplateDraftCache> _cache = new();
    private readonly Mock<IStorageService> _storage = new();
    private readonly Mock<ITemplateRepository> _templateRepo = new();
    private readonly Mock<IRepository<TemplateVersion>> _versionRepo = new();
    private readonly Mock<IUnitOfWork> _uow = new();

    private readonly Mock<IProjectRepository> _projectRepo = new();
    private readonly Mock<ISchemaInferenceService> _schemaInference = new();
    private readonly Mock<IExecutionContext> _context = new();
    private readonly Mock<ICurrentProjectContext> _projectContext = new();
    private readonly Microsoft.Extensions.Time.Testing.FakeTimeProvider _clock = TestConstants.CreateFakeClock();

    public CommitTemplateDraftUseCaseTests()
    {
        var sampleProject = Project.Create(Guid.NewGuid(), ProjectName.Create("Default Project"), TemplateSlug.Create("default-proj"), _clock.GetUtcNow());
        _projectRepo.Setup(p => p.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(sampleProject);
    }

    private CommitTemplateDraftUseCase CreateSut() =>
        new(
            _cache.Object,
            _storage.Object,
            _templateRepo.Object,
            _versionRepo.Object,
            _uow.Object,
            _projectRepo.Object,
            _schemaInference.Object,
            _context.Object,
            _projectContext.Object,
            _clock);

    [Fact]
    public async Task CommitAsync_HappyPath_UploadsToMinioThenWritesDb()
    {
        // Arrange
        var fileBytes = Encoding.UTF8.GetBytes("xlsx-content");
        var entry = new TemplateDraftEntry(fileBytes, "report.xlsx", ".xlsx", []);
        var request = new CommitDraftCommand("Report", "report", "finance", [], ProjectId: Guid.NewGuid());

        _cache.Setup(c => c.GetAsync("draft-commit", It.IsAny<CancellationToken>())).ReturnsAsync(entry);
        _storage.Setup(s => s.UploadAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Stream>(),
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("storage-key");
        _templateRepo.Setup(r => r.AddAsync(It.IsAny<Template>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _versionRepo.Setup(r => r.AddAsync(It.IsAny<TemplateVersion>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _uow.Setup(u => u.CommitAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        _cache.Setup(c => c.RemoveAsync("draft-commit", It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        // Act
        var templateId = await CreateSut().ExecuteAsync(new CommitTemplateDraftCommand("draft-commit", request), CancellationToken.None);

        // Assert
        templateId.Should().NotBeEmpty();
        _storage.Verify(s => s.UploadAsync(StorageBuckets.Templates, It.Is<string>(k => k.StartsWith($"templates/{request.ProjectId}/")), It.IsAny<Stream>(),
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", It.IsAny<CancellationToken>()),
            Times.Once);
        _uow.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
        _cache.Verify(c => c.RemoveAsync("draft-commit", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CommitAsync_DbWriteFails_RollsBackMinioUpload()
    {
        // Arrange
        var entry = new TemplateDraftEntry(Encoding.UTF8.GetBytes("data"), "t.docx", ".docx", []);
        var request = new CommitDraftCommand("T", "test-template", null, [], ProjectId: Guid.NewGuid());

        _cache.Setup(c => c.GetAsync("draft-fail", It.IsAny<CancellationToken>())).ReturnsAsync(entry);
        _storage.Setup(s => s.UploadAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Stream>(),
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("storage-key");
        _templateRepo.Setup(r => r.AddAsync(It.IsAny<Template>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _versionRepo.Setup(r => r.AddAsync(It.IsAny<TemplateVersion>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _uow.Setup(u => u.CommitAsync(It.IsAny<CancellationToken>())).ThrowsAsync(new Exception("DB error"));
        _storage.Setup(s => s.DeleteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act & Assert
        await CreateSut().Invoking(u => u.ExecuteAsync(new CommitTemplateDraftCommand("draft-fail", request), CancellationToken.None))
            .Should().ThrowAsync<Exception>().WithMessage("DB error");

        _storage.Verify(s => s.DeleteAsync(StorageBuckets.Templates, It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task CommitAsync_DraftNotFound_ThrowsDraftExpiredException()
    {
        // Arrange
        _cache.Setup(c => c.GetAsync("stale", It.IsAny<CancellationToken>())).ReturnsAsync((TemplateDraftEntry?)null);
        var request = new CommitDraftCommand("T", "test-template", null, [], ProjectId: Guid.NewGuid());

        // Act & Assert
        await CreateSut().Invoking(u => u.ExecuteAsync(new CommitTemplateDraftCommand("stale", request), CancellationToken.None))
            .Should().ThrowAsync<DraftExpiredException>();
    }

    [Fact]
    public async Task CommitAsync_WithMappings_PersistsAllFieldMappings()
    {
        // Arrange
        var entry = new TemplateDraftEntry(Encoding.UTF8.GetBytes("html"), "t.html", ".html", []);
        var mappings = new List<SaveFieldMappingItemDto>
        {
            new("name", "customer.name", "ชื่อลูกค้า", true, null, null, 0),
            new("total", "invoice.total", "ยอดรวม", false, null, null, 1),
        };
        var request = new CommitDraftCommand("Invoice", "invoice", null, mappings, ProjectId: Guid.NewGuid());

        _cache.Setup(c => c.GetAsync("draft-map", It.IsAny<CancellationToken>())).ReturnsAsync(entry);
        _storage.Setup(s => s.UploadAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Stream>(),
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("key");
        _templateRepo.Setup(r => r.AddAsync(It.IsAny<Template>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _versionRepo.Setup(r => r.AddAsync(It.IsAny<TemplateVersion>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _uow.Setup(u => u.CommitAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        _cache.Setup(c => c.RemoveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        // Act
        await CreateSut().ExecuteAsync(new CommitTemplateDraftCommand("draft-map", request), CancellationToken.None);

        // Assert
        _templateRepo.Verify(r => r.AddAsync(
            It.Is<Template>(t => t.FieldMappings.Count == 2),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CommitAsync_MissingProjectIdAndNoContext_ThrowsValidationExceptionWithoutUploading()
    {
        // Arrange
        var entry = new TemplateDraftEntry(Encoding.UTF8.GetBytes("data"), "report.docx", ".docx", []);
        var request = new CommitDraftCommand("Report", "report", null, [], ProjectId: null);

        _cache.Setup(c => c.GetAsync("draft-no-proj", It.IsAny<CancellationToken>())).ReturnsAsync(entry);

        // Act & Assert
        var act = () => CreateSut().ExecuteAsync(new CommitTemplateDraftCommand("draft-no-proj", request), CancellationToken.None);
        var ex = await act.Should().ThrowAsync<ValidationException>();
        ex.Which.Errors.Should().ContainKey("ProjectId");

        _storage.Verify(s => s.UploadAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task CommitAsync_NullProjectIdInRequest_FallsBackToCurrentProjectContext()
    {
        // Arrange
        var contextProjectId = Guid.NewGuid();
        var mockContext = new Mock<ICurrentProjectContext>();
        mockContext.Setup(c => c.IsResolved).Returns(true);
        mockContext.Setup(c => c.ProjectId).Returns(new SmkDoc.Domain.ValueObjects.Ids.ProjectId(contextProjectId));

        var sut = new CommitTemplateDraftUseCase(
            _cache.Object,
            _storage.Object,
            _templateRepo.Object,
            _versionRepo.Object,
            _uow.Object,
            _projectRepo.Object,
            _schemaInference.Object,
            _context.Object,
            mockContext.Object,
            _clock);

        var fileBytes = Encoding.UTF8.GetBytes("docx-content");
        var entry = new TemplateDraftEntry(fileBytes, "report.docx", ".docx", []);
        var request = new CommitDraftCommand("Report", "report", null, [], ProjectId: null);

        _cache.Setup(c => c.GetAsync("draft-ctx", It.IsAny<CancellationToken>())).ReturnsAsync(entry);
        _storage.Setup(s => s.UploadAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Stream>(),
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("storage-key");
        _templateRepo.Setup(r => r.AddAsync(It.IsAny<Template>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _versionRepo.Setup(r => r.AddAsync(It.IsAny<TemplateVersion>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _uow.Setup(u => u.CommitAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        _cache.Setup(c => c.RemoveAsync("draft-ctx", It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        // Act
        var result = await sut.ExecuteAsync(new CommitTemplateDraftCommand("draft-ctx", request), CancellationToken.None);

        // Assert
        result.Should().NotBeEmpty();
        _storage.Verify(s => s.UploadAsync(StorageBuckets.Templates, It.Is<string>(k => k.StartsWith($"templates/{contextProjectId}/")), It.IsAny<Stream>(),
                "application/vnd.openxmlformats-officedocument.wordprocessingml.document", It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
