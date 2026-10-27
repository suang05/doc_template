using System.Linq.Expressions;
using System.Text;
using System.Text.Json;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.Authoring.FieldMappings.DTOs;
using SmkDoc.Application.Modules.Rendering.Documents;
using SmkDoc.Application.Modules.Rendering.Documents.Commands.GenerateDocument;
using SmkDoc.Application.Modules.Rendering.Documents.DTOs;
using SmkDoc.Application.Modules.Rendering.Documents.Services;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Enums;
using SmkDoc.Domain.Exceptions;
using SmkDoc.Domain.Interfaces;
using SmkDoc.Domain.ValueObjects.Validation;
using SmkDoc.Tests.Common.Builders;

namespace SmkDoc.Tests.Application.Modules.Rendering.Documents.Commands.GenerateDocument;

public sealed class GenerateDocumentUseCaseTests
{
    private readonly ITemplateRepository _templateRepo = Substitute.For<ITemplateRepository>();
    private readonly IDatasetRepository _datasetRepo = Substitute.For<IDatasetRepository>();
    private readonly IDataConnectionRepository _connectionRepo = Substitute.For<IDataConnectionRepository>();
    private readonly IGenerationLogRepository _logRepo = Substitute.For<IGenerationLogRepository>();
    private readonly IDocumentRepository _documentRepo = Substitute.For<IDocumentRepository>();
    private readonly IStorageService _storageService = Substitute.For<IStorageService>();
    private readonly IRenderEngine _engine = Substitute.For<IRenderEngine>();
    private readonly IExecutionContext _executionContext = Substitute.For<IExecutionContext>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly IFieldMappingApplicatorService _applicator = Substitute.For<IFieldMappingApplicatorService>();
    private readonly IDataProtectionService _dataProtection = Substitute.For<IDataProtectionService>();
    private readonly IJsonSchemaValidationService _schemaValidation = Substitute.For<IJsonSchemaValidationService>();
    private readonly FakeTimeProvider _timeProvider = TestConstants.CreateFakeClock();

    public GenerateDocumentUseCaseTests()
    {
        _unitOfWork.CommitAsync(Arg.Any<CancellationToken>()).Returns(1);
        _executionContext.ProjectId.Returns(Guid.NewGuid());
    }

    private GenerateDocumentUseCase CreateSut()
    {
        var dataPrep = new DocumentDataPreparationService(
            _datasetRepo,
            _connectionRepo,
            _dataProtection,
            _applicator,
            _schemaValidation);

        var audit = new DocumentAuditService(
            _logRepo,
            _executionContext);

        var versioning = new DocumentVersioningService(
            _documentRepo,
            _executionContext);

        return new GenerateDocumentUseCase(
            _templateRepo,
            _storageService,
            new[] { _engine },
            dataPrep,
            audit,
            versioning,
            _unitOfWork,
            _executionContext,
            _timeProvider);
    }

    private void SetupTemplate(Template template)
    {
        _templateRepo.GetBySlugWithDetailsAsync(template.Slug.Value, Arg.Any<CancellationToken>())
            .Returns(template);
    }

    private void SetupInactiveTemplate(string slug)
    {
        Template template = TemplateBuilder.ATemplate().AsInactive().WithSlug(slug);
        SetupTemplate(template);
    }

    private void SetupTemplateWithoutVersion(string slug)
    {
        Template template = TemplateBuilder.ATemplate().WithoutCurrentVersion().WithSlug(slug);
        SetupTemplate(template);
    }

    private (Template Template, TemplateVersion Version) SetupActiveHtmlTemplate(
        string slug = "sale-contract",
        string name = "Sale Contract",
        string htmlContent = "<html><body>{{name}}</body></html>",
        int versionNumber = 1,
        Guid? templateId = null,
        Guid? versionId = null,
        DateTimeOffset? now = null)
    {
        var tId = templateId ?? Guid.NewGuid();
        var vId = versionId ?? Guid.NewGuid();
        var timestamp = now ?? TestConstants.BaselineTime;

        Template template = TemplateBuilder.ATemplate()
            .WithId(tId)
            .WithName(name)
            .WithSlug(slug)
            .WithCurrentVersion(vId)
            .WithTime(timestamp);

        TemplateVersion version = TemplateVersionBuilder.AVersion()
            .WithId(vId)
            .WithTemplateId(tId)
            .WithVersion(versionNumber)
            .WithStorageKey($"templates/{slug}.html")
            .WithFormat(TemplateFormat.Html)
            .WithTime(timestamp);

        SetupTemplateWithVersion(template, version, htmlContent);
        return (template, version);
    }

    private void SetupTemplateWithVersion(Template template, TemplateVersion version, string htmlContent = "<html></html>")
    {
        SetupTemplate(template);
        _templateRepo.GetVersionByIdAsync(version.Id, Arg.Any<CancellationToken>())
            .Returns(version);
        _storageService.DownloadAsync("templates", version.StorageKey, Arg.Any<CancellationToken>())
            .Returns(new MemoryStream(Encoding.UTF8.GetBytes(htmlContent)));
    }

    private void SetupRenderEnginePdfOutput(string output = "%PDF-1.4 Mock Output")
    {
        _engine.EngineType.Returns(RenderEngineType.Html);
        _engine.RenderStreamAsync(Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<OutputFormat>(), Arg.Any<CancellationToken>())
            .Returns(new MemoryStream(Encoding.UTF8.GetBytes(output)));
    }

    private void SetupPresignedUrl(string url = "https://minio.sammakorn.co.th/outputs/sample.pdf")
    {
        _storageService.GetPresignedUrlAsync("outputs", Arg.Any<string>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns(url);
    }

    [Fact]
    public async Task ExecuteAsync_WhenTemplateActive_ShouldGenerateAndReturnPresignedUrl()
    {
        // Arrange
        var (template, currentVersion) = SetupActiveHtmlTemplate(
            slug: "sale-contract",
            name: "Sale Contract",
            htmlContent: "<html><body>{{name}}</body></html>",
            versionNumber: 2);
        var templateId = template.Id;
        var versionId = currentVersion.Id;

        SetupRenderEnginePdfOutput("%PDF-1.4 Mock Output");
        SetupPresignedUrl("https://minio.sammakorn.co.th/outputs/sample.pdf?signature=valid");

        // No existing Document for this ref
        _documentRepo.GetByDocumentRefWithVersionsAsync(Arg.Any<DocumentReference>(), Arg.Any<CancellationToken>())
            .Returns((Document?)null);

        _executionContext.CallerApp.Returns("sales-app");

        using var jsonDoc = JsonDocument.Parse("{\"name\": \"สมชาย ใจดี\"}");
        var request = new GenerateDocumentCommand(jsonDoc.RootElement, OutputFormat: "pdf", DocumentRef: "SC-2026-0001");

        // Act
        GenerateDocumentResultDto result = await CreateSut().ExecuteAsync("sale-contract", request);

        // Assert
        result.Should().NotBeNull();
        result.Url.Should().StartWith("https://minio.sammakorn.co.th");
        result.OutputFormat.Should().Be("pdf");
        result.GenerationId.Version.Should().Be(7);

        await _logRepo.Received(1).AddAsync(
            Arg.Is<GenerationLog>(l => l.TemplateId == templateId
                                   && l.TemplateVersionId == versionId
                                   && l.Status == "SUCCESS"
                                   && l.TriggerSource == "api"),
            Arg.Any<CancellationToken>());

        await _documentRepo.Received(1).AddAsync(
            Arg.Is<Document>(d => d.DocumentRef == "SC-2026-0001" && d.Versions.Count == 1 && d.Versions.First().Version == 1),
            Arg.Any<CancellationToken>());

        await _unitOfWork.Received(1).CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenTemplateInactive_ShouldThrowNotFoundException()
    {
        // Arrange
        SetupInactiveTemplate("inactive-tpl");

        using var jsonDoc = JsonDocument.Parse("{}");
        var request = new GenerateDocumentCommand(jsonDoc.RootElement);

        // Act & Assert
        Func<Task> act = () => CreateSut().ExecuteAsync("inactive-tpl", request);
        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task ExecuteAsync_WhenNoCurrentVersion_ShouldThrowBusinessRuleViolationException()
    {
        // Arrange
        SetupTemplateWithoutVersion("no-ver-tpl");

        using var jsonDoc = JsonDocument.Parse("{}");
        var request = new GenerateDocumentCommand(jsonDoc.RootElement);

        // Act & Assert
        Func<Task> act = () => CreateSut().ExecuteAsync("no-ver-tpl", request);
        await act.Should().ThrowAsync<BusinessRuleViolationException>();
    }

    [Fact]
    public async Task ExecuteAsync_WithFieldMappings_ShouldTransformAndMergeData()
    {
        // Arrange
        var now = TestConstants.BaselineTime;
        var (template, currentVersion) = SetupActiveHtmlTemplate(
            slug: "contract-mapped",
            name: "Contract With Mappings",
            htmlContent: "<html><body>{{amount_baht}}</body></html>",
            now: now);
        var templateId = template.Id;

        var mappings = new List<FieldMapping>
        {
            FieldMapping.Create(templateId, "amount_baht", "contract.price", "Price in Baht Text", true, 1, now, DataSourceType.Json)
        };

        mappings[0].UpdateMappingDetails("contract.price", "Price in Baht Text", true, null, "baht", 1, now);
        template.ReplaceFieldMappings(mappings, now);

        _applicator.ApplyAsync(
                Arg.Any<JsonElement>(),
                Arg.Any<IEnumerable<FieldMapping>>(),
                Arg.Any<IReadOnlyDictionary<string, ResolvedDatasetContext>>())
            .Returns("{\"amount_baht\":\"สองล้านห้าแสนบาทถ้วน\"}");

        string capturedDataJson = string.Empty;
        _engine.EngineType.Returns(RenderEngineType.Html);
        _engine.RenderStreamAsync(Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<OutputFormat>(), Arg.Any<CancellationToken>())
            .Returns(x =>
            {
                capturedDataJson = x.ArgAt<string>(1);
                return new MemoryStream(Encoding.UTF8.GetBytes("%PDF-1.4 Mock Output"));
            });

        SetupPresignedUrl("https://minio.sammakorn.co.th/outputs/sample.pdf");

        using var jsonDoc = JsonDocument.Parse("{\"contract\": {\"price\": \"2500000\"}}");
        var request = new GenerateDocumentCommand(jsonDoc.RootElement);

        // Act
        GenerateDocumentResultDto result = await CreateSut().ExecuteAsync("contract-mapped", request);

        // Assert
        result.Should().NotBeNull();
        result.Sha256.Should().NotBeNullOrEmpty();
        result.FileSizeBytes.Should().BeGreaterThan(0);
        capturedDataJson.Should().Contain("สองล้านห้าแสนบาทถ้วน");
        capturedDataJson.Should().Contain("amount_baht");
    }

    [Fact]
    public async Task ExecuteAsync_WithExistingDocumentRef_ShouldReuseDocumentAndIncrementVersion()
    {
        // Arrange
        var now = TestConstants.BaselineTime;
        var (template, currentVersion) = SetupActiveHtmlTemplate(
            slug: "sale-contract",
            name: "Contract",
            now: now);
        var templateId = template.Id;
        var documentId = Guid.NewGuid();

        Document existingDocument = new DocumentBuilder()
            .WithId(documentId)
            .WithDocumentRef("SC-2026-0001")
            .WithTemplateId(templateId)
            .WithTime(now);

        SetupRenderEnginePdfOutput("%PDF-1.4");
        SetupPresignedUrl("https://minio.sammakorn.co.th/outputs/sc.pdf");

        existingDocument.CreateVersion(Guid.NewGuid(), Guid.NewGuid(), "v1", "app", now);
        existingDocument.CreateVersion(Guid.NewGuid(), Guid.NewGuid(), "v2", "app", now);

        // Existing document found
        _documentRepo.GetByDocumentRefWithVersionsAsync(Arg.Any<DocumentReference>(), Arg.Any<CancellationToken>())
            .Returns(existingDocument);

        using var jsonDoc = JsonDocument.Parse("{}");
        var request = new GenerateDocumentCommand(jsonDoc.RootElement, OutputFormat: "pdf", DocumentRef: "SC-2026-0001");

        // Act
        GenerateDocumentResultDto result = await CreateSut().ExecuteAsync("sale-contract", request);

        // Assert — Document NOT created again
        result.DocumentRef.Should().Be("SC-2026-0001");
        result.Sha256.Should().NotBeNullOrEmpty();
        await _documentRepo.DidNotReceive().AddAsync(Arg.Any<Document>(), Arg.Any<CancellationToken>());
        existingDocument.Versions.Should().HaveCount(3);
        existingDocument.Versions.Last().Version.Should().Be(3);
    }

    [Fact]
    public async Task ExecuteAsync_WhenPayloadFailsSchema_ShouldThrowSchemaValidationExceptionAndLogFailed()
    {
        // Arrange
        const string schemaJson = """{"$schema":"http://json-schema.org/draft-07/schema#","type":"object","required":["doc_no"]}""";
        var now = TestConstants.BaselineTime;

        var (template, currentVersion) = SetupActiveHtmlTemplate(
            slug: "invoice",
            name: "Invoice",
            now: now);

        currentVersion.UpdateDataSchema(schemaJson, null, now);

        // Service returns invalid result (missing required field "doc_no")
        var errors = new List<ValidationErrorItem>
        {
            new("/", "required", "Required property 'doc_no' not found in JSON.")
        };
        _schemaValidation.Validate(schemaJson, Arg.Any<string>())
            .Returns(SchemaValidationResult.Failure(errors));

        // Engine and storage must be set up so the use case reaches the validation gate
        _engine.EngineType.Returns(RenderEngineType.Html);
        _storageService.DownloadAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new MemoryStream());

        _unitOfWork.CommitAsync(Arg.Any<CancellationToken>())
            .Returns(1);

        using var jsonDoc = JsonDocument.Parse("""{"customer":"ACME"}"""); // missing doc_no
        var request = new GenerateDocumentCommand(jsonDoc.RootElement, OutputFormat: "pdf");

        // Act
        var act = () => CreateSut().ExecuteAsync("invoice", request);

        // Assert — SchemaValidationException is thrown
        var ex = await act.Should().ThrowAsync<SchemaValidationException>();
        ex.Which.TemplateSlug.Should().Be("invoice");
        ex.Which.Version.Should().Be(1);
        ex.Which.Errors.Should().HaveCount(1);
        ex.Which.Errors[0].Rule.Should().Be("required");

        // Assert — VALIDATION_FAILED logged (no rendering attempted)
        await _logRepo.Received(1).AddAsync(
            Arg.Is<GenerationLog>(l => l.Status == "VALIDATION_FAILED"),
            Arg.Any<CancellationToken>());
        await _engine.DidNotReceive().RenderStreamAsync(
            Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<OutputFormat>(), Arg.Any<CancellationToken>());
        await _storageService.DidNotReceive().DownloadAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenSkipValidationTrue_ShouldBypassSchemaAndRender()
    {
        // Arrange
        const string schemaJson = """{"$schema":"http://json-schema.org/draft-07/schema#","type":"object","required":["doc_no"]}""";
        var now = TestConstants.BaselineTime;

        var (template, currentVersion) = SetupActiveHtmlTemplate(
            slug: "invoice",
            name: "Invoice",
            now: now);
        currentVersion.UpdateDataSchema(schemaJson, null, now);

        SetupRenderEnginePdfOutput();
        _storageService.UploadAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns("outputs/invoice.pdf");
        SetupPresignedUrl("https://example.com/download/invoice.pdf");

        using var jsonDoc = JsonDocument.Parse("""{"customer":"ACME"}"""); // missing doc_no — but SkipValidation
        var request = new GenerateDocumentCommand(jsonDoc.RootElement, OutputFormat: "pdf", SkipValidation: true);

        // Act
        GenerateDocumentResultDto result = await CreateSut().ExecuteAsync("invoice", request);

        // Assert — validation service never called; render completed successfully
        _schemaValidation.DidNotReceive().Validate(Arg.Any<string>(), Arg.Any<string>());
        await _engine.Received(1).RenderStreamAsync(Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<OutputFormat>(), Arg.Any<CancellationToken>());
        result.Url.Should().Be("https://example.com/download/invoice.pdf");
    }

    [Fact]
    public async Task ExecuteAsync_WhenOutputFormatIsUnrecognized_FallbacksToPdf()
    {
        // Arrange
        SetupActiveHtmlTemplate(
            slug: "fallback-contract",
            name: "Fallback Contract",
            htmlContent: "<html><body>Fallback</body></html>");
        SetupRenderEnginePdfOutput("%PDF-1.4 Mock Output");
        SetupPresignedUrl("https://example.com/download/fallback.pdf");

        using var jsonDoc = JsonDocument.Parse("{}");
        var request = new GenerateDocumentCommand(jsonDoc.RootElement, OutputFormat: "unknown_format", SkipValidation: true);

        // Act
        GenerateDocumentResultDto result = await CreateSut().ExecuteAsync("fallback-contract", request);

        // Assert — verifies engine was called with OutputFormat.Pdf fallback
        await _engine.Received(1).RenderStreamAsync(
            Arg.Any<Stream>(),
            Arg.Any<string>(),
            OutputFormat.Pdf,
            Arg.Any<CancellationToken>());
        result.Url.Should().Be("https://example.com/download/fallback.pdf");
    }
}
