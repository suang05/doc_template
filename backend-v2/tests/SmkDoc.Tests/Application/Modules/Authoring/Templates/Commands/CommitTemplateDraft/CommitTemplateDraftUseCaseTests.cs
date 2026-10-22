using System.Text;
using SmkDoc.Application.Common;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.Authoring.FieldMappings.DTOs;
using SmkDoc.Application.Modules.Authoring.Templates.Commands.CommitTemplateDraft;
using SmkDoc.Application.Modules.Authoring.Templates.DTOs;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Exceptions;
using SmkDoc.Domain.Interfaces;
using SmkDoc.Domain.ValueObjects;
using SmkDoc.Domain.ValueObjects.Ids;
using ValidationException = SmkDoc.Application.Common.Exceptions.ValidationException;

namespace SmkDoc.Tests.Application.Modules.Authoring.Templates.Commands.CommitTemplateDraft;

public sealed class CommitTemplateDraftUseCaseTests
{
    private readonly ITemplateDraftCache _draftCache = Substitute.For<ITemplateDraftCache>();
    private readonly IStorageService _storageService = Substitute.For<IStorageService>();
    private readonly ITemplateRepository _templateRepo = Substitute.For<ITemplateRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly IProjectRepository _projectRepo = Substitute.For<IProjectRepository>();
    private readonly ISchemaInferenceService _schemaInference = Substitute.For<ISchemaInferenceService>();
    private readonly IExecutionContext _executionContext = Substitute.For<IExecutionContext>();
    private readonly ICurrentProjectContext _projectContext = Substitute.For<ICurrentProjectContext>();
    private readonly FakeTimeProvider _timeProvider = TestConstants.CreateFakeClock();

    public CommitTemplateDraftUseCaseTests()
    {
        _executionContext.UserId.Returns(TestConstants.DefaultUserId);
        _unitOfWork.CommitAsync(Arg.Any<CancellationToken>()).Returns(1);

        Project sampleProject = Project.Create(
            Guid.NewGuid(),
            ProjectName.Create("Default Project"),
            TemplateSlug.Create("default-proj"),
            _timeProvider.GetUtcNow());

        _projectRepo.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(sampleProject);
    }

    private CommitTemplateDraftUseCase CreateSut() => new(
        _draftCache,
        _storageService,
        _templateRepo,
        _unitOfWork,
        _projectRepo,
        _schemaInference,
        _executionContext,
        _projectContext,
        _timeProvider);

    [Fact]
    public async Task ExecuteAsync_WhenValidDraft_ShouldUploadToMinioAndPersistToDb()
    {
        // Arrange
        var fileBytes = Encoding.UTF8.GetBytes("xlsx-content");
        var entry = new TemplateDraftEntry(fileBytes, "report.xlsx", ".xlsx", []);
        var request = new CommitDraftCommand("Report", "report", "finance", [], ProjectId: Guid.NewGuid());

        _draftCache.GetAsync("draft-commit", Arg.Any<CancellationToken>()).Returns(entry);
        _storageService.UploadAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns("storage-key");

        // Act
        var templateId = await CreateSut().ExecuteAsync(new CommitTemplateDraftCommand("draft-commit", request), CancellationToken.None);

        // Assert
        templateId.Should().NotBeEmpty();
        await _storageService.Received(1).UploadAsync(
            StorageBuckets.Templates,
            Arg.Is<string>(k => k.StartsWith($"templates/{request.ProjectId}/")),
            Arg.Any<Stream>(),
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).CommitAsync(Arg.Any<CancellationToken>());
        await _draftCache.Received(1).RemoveAsync("draft-commit", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenDbWriteFails_ShouldRollbackMinioUpload()
    {
        // Arrange
        var entry = new TemplateDraftEntry(Encoding.UTF8.GetBytes("data"), "t.docx", ".docx", []);
        var request = new CommitDraftCommand("T", "test-template", null, [], ProjectId: Guid.NewGuid());

        _draftCache.GetAsync("draft-fail", Arg.Any<CancellationToken>()).Returns(entry);
        _storageService.UploadAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns("storage-key");
        _unitOfWork.CommitAsync(Arg.Any<CancellationToken>()).ThrowsAsync(new InvalidOperationException("DB error"));

        // Act & Assert
        await CreateSut().Invoking(u => u.ExecuteAsync(new CommitTemplateDraftCommand("draft-fail", request), CancellationToken.None))
            .Should().ThrowAsync<InvalidOperationException>().WithMessage("DB error");

        await _storageService.Received(1).DeleteAsync(StorageBuckets.Templates, Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenDraftNotFound_ShouldThrowDraftExpiredException()
    {
        // Arrange
        _draftCache.GetAsync("stale", Arg.Any<CancellationToken>()).Returns((TemplateDraftEntry?)null);
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

        _draftCache.GetAsync("draft-map", Arg.Any<CancellationToken>()).Returns(entry);
        _storageService.UploadAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns("key");

        // Act
        await CreateSut().ExecuteAsync(new CommitTemplateDraftCommand("draft-map", request), CancellationToken.None);

        // Assert
        await _templateRepo.Received(1).AddAsync(
            Arg.Is<Template>(t => t.FieldMappings.Count == 2),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenMissingProjectIdAndNoContext_ShouldThrowValidationExceptionWithoutUploading()
    {
        // Arrange
        var entry = new TemplateDraftEntry(Encoding.UTF8.GetBytes("data"), "report.docx", ".docx", []);
        var request = new CommitDraftCommand("Report", "report", null, [], ProjectId: null);

        _draftCache.GetAsync("draft-no-proj", Arg.Any<CancellationToken>()).Returns(entry);

        // Act & Assert
        var act = () => CreateSut().ExecuteAsync(new CommitTemplateDraftCommand("draft-no-proj", request), CancellationToken.None);
        var ex = await act.Should().ThrowAsync<ValidationException>();
        ex.Which.Errors.Should().ContainKey("ProjectId");

        await _storageService.DidNotReceive().UploadAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenNullProjectIdInRequest_ShouldFallbackToCurrentProjectContext()
    {
        // Arrange
        var contextProjectId = Guid.NewGuid();
        _projectContext.IsResolved.Returns(true);
        _projectContext.ProjectId.Returns(new ProjectId(contextProjectId));

        var fileBytes = Encoding.UTF8.GetBytes("docx-content");
        var entry = new TemplateDraftEntry(fileBytes, "report.docx", ".docx", []);
        var request = new CommitDraftCommand("Report", "report", null, [], ProjectId: null);

        _draftCache.GetAsync("draft-ctx", Arg.Any<CancellationToken>()).Returns(entry);
        _storageService.UploadAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns("storage-key");

        // Act
        var result = await CreateSut().ExecuteAsync(new CommitTemplateDraftCommand("draft-ctx", request), CancellationToken.None);

        // Assert
        result.Should().NotBeEmpty();
        await _storageService.Received(1).UploadAsync(
            StorageBuckets.Templates,
            Arg.Is<string>(k => k.StartsWith($"templates/{contextProjectId}/")),
            Arg.Any<Stream>(),
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            Arg.Any<CancellationToken>());
    }
}
