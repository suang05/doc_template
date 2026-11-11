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
using SmkDoc.Tests.Common.Builders;

namespace SmkDoc.Tests.Application.Modules.Authoring.Templates.Commands.CommitTemplateDraft;

public sealed class CommitTemplateDraftUseCaseTests
{
    private readonly ITemplateDraftCache _draftCache = Substitute.For<ITemplateDraftCache>();
    private readonly IStorageService _storageService = Substitute.For<IStorageService>();
    private readonly ITemplateRepository _templateRepo = Substitute.For<ITemplateRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
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
    }

    private CommitTemplateDraftUseCase CreateSut() => new(
        _draftCache,
        _storageService,
        _templateRepo,
        _unitOfWork,
        _schemaInference,
        _projectContext,
        _timeProvider);

    [Fact]
    public async Task ExecuteAsync_WhenValidDraft_ShouldUploadImmutableVersionKeyAndPersistToDb()
    {
        // Arrange
        var entry = new TemplateDraftEntry("draft-commit", _defaultProjectId.Value, "drafts/report.xlsx", "report.xlsx", ".xlsx", []);
        var command = new CommitTemplateDraftCommand("draft-commit", "Report", "report", "finance", []);

        _draftCache.GetAsync(_defaultProjectId, "draft-commit", Arg.Any<CancellationToken>()).Returns(entry);
        _draftCache.OpenStreamAsync(entry, Arg.Any<CancellationToken>()).Returns(new MemoryStream(Encoding.UTF8.GetBytes("xlsx-content")));
        _storageService.UploadAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(ci => ci.ArgAt<string>(1));

        // Act
        var result = await CreateSut().ExecuteAsync(command, CancellationToken.None);

        // Assert
        result.TemplateId.Should().NotBeEmpty();
        var templateGuid = result.TemplateId;
        var expectedKey = $"templates/{_defaultProjectId.Value:N}/{templateGuid:N}/v1.xlsx";

        await _storageService.Received(1).UploadAsync(
            StorageBuckets.Templates,
            expectedKey,
            Arg.Any<Stream>(),
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            Arg.Any<CancellationToken>());
        await _templateRepo.Received(1).AddAsync(
            Arg.Is<Template>(t =>
                t.Versions.Single().MappingsSnapshot == "[]"),
            Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).CommitAsync(Arg.Any<CancellationToken>());
        await _draftCache.Received(1).RemoveAsync(_defaultProjectId, "draft-commit", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenNoMappingsProvided_ShouldFallbackToDraftPlaceholdersAndSetEmptyMappingsSnapshot()
    {
        // Arrange
        var placeholders = new List<string> { "invoice_no", "customer_name" };
        var entry = new TemplateDraftEntry("draft-fallback", _defaultProjectId.Value, "drafts/contract.docx", "contract.docx", ".docx", placeholders);
        var command = new CommitTemplateDraftCommand("draft-fallback", "Contract", "contract", "legal", []);

        _draftCache.GetAsync(_defaultProjectId, "draft-fallback", Arg.Any<CancellationToken>()).Returns(entry);
        _draftCache.OpenStreamAsync(entry, Arg.Any<CancellationToken>()).Returns(new MemoryStream(Encoding.UTF8.GetBytes("docx-content")));
        _storageService.UploadAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(ci => ci.ArgAt<string>(1));
        _schemaInference.InferSchemaFromPlaceholders(Arg.Any<IEnumerable<string>>(), Arg.Any<string?>(), Arg.Any<string?>())
            .Returns("{\"$schema\":\"mock-schema\"}");
        _schemaInference.GenerateDefaultSamplePayload(Arg.Any<IEnumerable<string>>())
            .Returns("{\"invoice_no\":\"INV-001\"}");

        // Act
        var result = await CreateSut().ExecuteAsync(command, CancellationToken.None);

        // Assert
        result.TemplateId.Should().NotBeEmpty();
        _schemaInference.Received(1).InferSchemaFromPlaceholders(
            Arg.Is<IEnumerable<string>>(p => p.SequenceEqual(placeholders)),
            null,
            "contract");
        await _templateRepo.Received(1).AddAsync(
            Arg.Is<Template>(t =>
                t.Versions.Single().MappingsSnapshot == "[]" &&
                t.Versions.Single().DataSchema == "{\"$schema\":\"mock-schema\"}" &&
                t.Versions.Single().SamplePayload == "{\"invoice_no\":\"INV-001\"}"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenDbWriteFails_ShouldRollbackMinioUpload()
    {
        // Arrange
        var entry = new TemplateDraftEntry("draft-fail", _defaultProjectId.Value, "drafts/t.docx", "t.docx", ".docx", []);
        var command = new CommitTemplateDraftCommand("draft-fail", "Report", "test-template", null, []);

        _draftCache.GetAsync(_defaultProjectId, "draft-fail", Arg.Any<CancellationToken>()).Returns(entry);
        _draftCache.OpenStreamAsync(entry, Arg.Any<CancellationToken>()).Returns(new MemoryStream(Encoding.UTF8.GetBytes("docx-content")));
        _storageService.UploadAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(ci => ci.ArgAt<string>(1));
        _unitOfWork.CommitAsync(Arg.Any<CancellationToken>()).ThrowsAsync(new InvalidOperationException("DB error"));

        // Act & Assert
        await CreateSut().Invoking(u => u.ExecuteAsync(command, CancellationToken.None))
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
        _draftCache.GetAsync(_defaultProjectId, "stale", Arg.Any<CancellationToken>()).Returns((TemplateDraftEntry?)null);
        var command = new CommitTemplateDraftCommand("stale", "Report", "test-template", null, []);

        // Act & Assert
        await CreateSut().Invoking(u => u.ExecuteAsync(command, CancellationToken.None))
            .Should().ThrowAsync<DraftExpiredException>();
    }

    [Fact]
    public async Task ExecuteAsync_WhenMappingsProvided_ShouldPersistFieldMappingsAndMappingsSnapshot()
    {
        // Arrange
        var entry = new TemplateDraftEntry("draft-map", _defaultProjectId.Value, "drafts/t.html", "t.html", ".html", []);
        var mappings = new List<SaveFieldMappingItemDto>
        {
            new("name", "customer.name", "ชื่อลูกค้า", true, null, null, 0),
            new("total", "invoice.total", "ยอดรวม", false, null, null, 1),
        };
        var command = new CommitTemplateDraftCommand("draft-map", "Invoice", "invoice", null, mappings);

        _draftCache.GetAsync(_defaultProjectId, "draft-map", Arg.Any<CancellationToken>()).Returns(entry);
        _draftCache.OpenStreamAsync(entry, Arg.Any<CancellationToken>()).Returns(new MemoryStream(Encoding.UTF8.GetBytes("html-content")));
        _storageService.UploadAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(ci => ci.ArgAt<string>(1));

        // Act
        await CreateSut().ExecuteAsync(command, CancellationToken.None);

        // Assert
        await _templateRepo.Received(1).AddAsync(
            Arg.Is<Template>(t =>
                t.FieldMappings.Count == 2 &&
                !string.IsNullOrWhiteSpace(t.Versions.Single().MappingsSnapshot) &&
                t.Versions.Single().MappingsSnapshot!.Contains("customer.name")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenProjectContextUnresolved_ShouldThrowTenantContextRequiredExceptionWithoutUploading()
    {
        // Arrange
        _projectContext.IsResolved.Returns(false);
        _projectContext.ProjectId.Returns(_ => throw new TenantContextRequiredException());
        var entry = new TemplateDraftEntry("draft-no-proj", _defaultProjectId.Value, "drafts/report.docx", "report.docx", ".docx", []);
        var command = new CommitTemplateDraftCommand("draft-no-proj", "Report", "report", null, []);

        _draftCache.GetAsync(Arg.Any<ProjectId>(), "draft-no-proj", Arg.Any<CancellationToken>()).Returns(entry);

        // Act & Assert
        var act = () => CreateSut().ExecuteAsync(command, CancellationToken.None);
        await act.Should().ThrowAsync<TenantContextRequiredException>();

        await _storageService.DidNotReceive().UploadAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }
}
