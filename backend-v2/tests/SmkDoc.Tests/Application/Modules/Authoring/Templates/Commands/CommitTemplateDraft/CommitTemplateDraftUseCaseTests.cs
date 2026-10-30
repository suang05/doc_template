using System.Text;
using SmkDoc.Application.Common;
using SmkDoc.Application.Common.Exceptions;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.Authoring.FieldMappings.DTOs;
using SmkDoc.Application.Modules.Authoring.Templates.Commands.CommitTemplateDraft;
using SmkDoc.Application.Modules.Authoring.Templates.DTOs;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Exceptions;
using SmkDoc.Domain.Interfaces;
using SmkDoc.Domain.ValueObjects;
using SmkDoc.Domain.ValueObjects.Ids;

namespace SmkDoc.Tests.Application.Modules.Authoring.Templates.Commands.CommitTemplateDraft;

public sealed class CommitTemplateDraftUseCaseTests
{
    private readonly ITemplateDraftCache _draftCache = Substitute.For<ITemplateDraftCache>();
    private readonly IStorageService _storageService = Substitute.For<IStorageService>();
    private readonly ITemplateRepository _templateRepo = Substitute.For<ITemplateRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly IProjectRepository _projectRepo = Substitute.For<IProjectRepository>();
    private readonly ISchemaInferenceService _schemaInference = Substitute.For<ISchemaInferenceService>();
    private readonly ICurrentProjectContext _projectContext = Substitute.For<ICurrentProjectContext>();
    private readonly FakeTimeProvider _timeProvider = TestConstants.CreateFakeClock();
    private readonly ProjectId _defaultProjectId = ProjectId.New();

    public CommitTemplateDraftUseCaseTests()
    {
        _unitOfWork.CommitAsync(Arg.Any<CancellationToken>()).Returns(1);
        _projectContext.IsResolved.Returns(true);
        _projectContext.ProjectId.Returns(_defaultProjectId);
        _projectContext.CallerIdentity.Returns("studio-user");

        Project sampleProject = Project.Create(
            CompanyId.New(),
            ProjectName.Create("Default Project"),
            ProjectSlug.Create("default-proj"),
            _timeProvider.GetUtcNow());

        _projectRepo.GetByIdAsync(Arg.Any<ProjectId>(), Arg.Any<CancellationToken>())
            .Returns(sampleProject);
    }

    private CommitTemplateDraftUseCase CreateSut() => new(
        _draftCache,
        _storageService,
        _templateRepo,
        _unitOfWork,
        _projectRepo,
        _schemaInference,
        _projectContext,
        _timeProvider);

    [Fact]
    public async Task ExecuteAsync_WhenValidDraft_ShouldUploadImmutableVersionKeyAndPersistToDb()
    {
        // Arrange
        var fileBytes = Encoding.UTF8.GetBytes("xlsx-content");
        var entry = new TemplateDraftEntry(fileBytes, "report.xlsx", ".xlsx", []);
        var request = new CommitDraftCommand("Report", "report", "finance", []);

        _draftCache.GetAsync("draft-commit", Arg.Any<CancellationToken>()).Returns(entry);
        _storageService.UploadAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(ci => ci.ArgAt<string>(1));

        // Act
        var templateIdStr = await CreateSut().ExecuteAsync(new CommitTemplateDraftCommand("draft-commit", request), CancellationToken.None);

        // Assert
        templateIdStr.Should().NotBeEmpty();
        var templateGuid = Guid.Parse(templateIdStr);
        var expectedKey = $"templates/{_defaultProjectId.Value:N}/{templateGuid:N}/v1.xlsx";

        await _storageService.Received(1).UploadAsync(
            StorageBuckets.Templates,
            expectedKey,
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
        var request = new CommitDraftCommand("Report", "test-template", null, []);

        _draftCache.GetAsync("draft-fail", Arg.Any<CancellationToken>()).Returns(entry);
        _storageService.UploadAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(ci => ci.ArgAt<string>(1));
        _unitOfWork.CommitAsync(Arg.Any<CancellationToken>()).ThrowsAsync(new InvalidOperationException("DB error"));

        // Act & Assert
        await CreateSut().Invoking(u => u.ExecuteAsync(new CommitTemplateDraftCommand("draft-fail", request), CancellationToken.None))
            .Should().ThrowAsync<InvalidOperationException>().WithMessage("DB error");

        await _storageService.Received(1).DeleteAsync(
            StorageBuckets.Templates,
            Arg.Is<string>(k => k.StartsWith($"templates/{_defaultProjectId.Value:N}/") && k.EndsWith("/v1.docx")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenDraftNotFound_ShouldThrowDraftExpiredException()
    {
        // Arrange
        _draftCache.GetAsync("stale", Arg.Any<CancellationToken>()).Returns((TemplateDraftEntry?)null);
        var request = new CommitDraftCommand("Report", "test-template", null, []);

        // Act & Assert
        await CreateSut().Invoking(u => u.ExecuteAsync(new CommitTemplateDraftCommand("stale", request), CancellationToken.None))
            .Should().ThrowAsync<DraftExpiredException>();
    }

    [Fact]
    public async Task ExecuteAsync_WhenMappingsProvided_ShouldPersistFieldMappingsAndMappingsSnapshot()
    {
        // Arrange
        var entry = new TemplateDraftEntry(Encoding.UTF8.GetBytes("html"), "t.html", ".html", []);
        var mappings = new List<SaveFieldMappingItemDto>
        {
            new("name", "customer.name", "ชื่อลูกค้า", true, null, null, 0),
            new("total", "invoice.total", "ยอดรวม", false, null, null, 1),
        };
        var request = new CommitDraftCommand("Invoice", "invoice", null, mappings);

        _draftCache.GetAsync("draft-map", Arg.Any<CancellationToken>()).Returns(entry);
        _storageService.UploadAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(ci => ci.ArgAt<string>(1));

        // Act
        await CreateSut().ExecuteAsync(new CommitTemplateDraftCommand("draft-map", request), CancellationToken.None);

        // Assert
        await _templateRepo.Received(1).AddAsync(
            Arg.Is<Template>(t =>
                t.FieldMappings.Count == 2 &&
                !string.IsNullOrWhiteSpace(t.Versions.Single().MappingsSnapshot)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenProjectContextUnresolved_ShouldThrowForbiddenExceptionWithoutUploading()
    {
        // Arrange
        _projectContext.IsResolved.Returns(false);
        var entry = new TemplateDraftEntry(Encoding.UTF8.GetBytes("data"), "report.docx", ".docx", []);
        var request = new CommitDraftCommand("Report", "report", null, []);

        _draftCache.GetAsync("draft-no-proj", Arg.Any<CancellationToken>()).Returns(entry);

        // Act & Assert
        var act = () => CreateSut().ExecuteAsync(new CommitTemplateDraftCommand("draft-no-proj", request), CancellationToken.None);
        await act.Should().ThrowAsync<ForbiddenException>()
            .WithMessage("*active project context*");

        await _storageService.DidNotReceive().UploadAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }
}
