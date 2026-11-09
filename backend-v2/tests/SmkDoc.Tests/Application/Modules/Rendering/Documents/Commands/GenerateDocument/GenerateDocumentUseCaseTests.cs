using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using SmkDoc.Application.Common;
using SmkDoc.Application.Common.Behaviors;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.Authoring.FieldMappings.DTOs;
using SmkDoc.Application.Modules.Rendering.Documents;
using SmkDoc.Application.Modules.Rendering.Documents.Commands.GenerateDocument;
using SmkDoc.Application.Modules.Rendering.Documents.DTOs;
using SmkDoc.Application.Modules.Rendering.Documents.Services;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Enums;
using SmkDoc.Domain.Exceptions;
using SmkDoc.Application.Modules.DataBinding.DTOs;
using SmkDoc.Application.Modules.DataBinding.Services;
using SmkDoc.Domain.Interfaces;
using SmkDoc.Domain.ValueObjects;
using SmkDoc.Domain.ValueObjects.Ids;
using SmkDoc.Domain.ValueObjects.Validation;
using SmkDoc.Tests.Common.Builders;

namespace SmkDoc.Tests.Application.Modules.Rendering.Documents.Commands.GenerateDocument;

public sealed class GenerateDocumentUseCaseTests
{
    private readonly ITemplateRepository _templateRepo = Substitute.For<ITemplateRepository>();
    private readonly IGenerationLogQueue _logQueue = Substitute.For<IGenerationLogQueue>();
    private readonly IDocumentRepository _documentRepo = Substitute.For<IDocumentRepository>();
    private readonly IStorageService _storageService = Substitute.For<IStorageService>();
    private readonly IRenderEngine _engine = Substitute.For<IRenderEngine>();
    private readonly IRenderEngineFactory _engineFactory = Substitute.For<IRenderEngineFactory>();
    private readonly ICurrentProjectContext _projectContext = Substitute.For<ICurrentProjectContext>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly IJsonSchemaValidationService _schemaValidation = Substitute.For<IJsonSchemaValidationService>();
    private readonly IDataBindingEngine _dataBindingEngine = Substitute.For<IDataBindingEngine>();
    private readonly FakeTimeProvider _timeProvider = TestConstants.CreateFakeClock();

    public GenerateDocumentUseCaseTests()
    {
        _engineFactory.GetEngine(Arg.Any<RenderEngineType>()).Returns(_engine);
        _unitOfWork.CommitAsync(Arg.Any<CancellationToken>()).Returns(1);
        var projectId = Guid.NewGuid();
        _projectContext.IsResolved.Returns(true);
        _projectContext.ProjectId.Returns(new ProjectId(projectId));
        _projectContext.CallerIdentity.Returns("sales-app");

        _dataBindingEngine.BindAndValidateAsync(
            Arg.Any<Template>(),
            Arg.Any<TemplateVersion>(),
            Arg.Any<JsonElement>(),
            Arg.Any<bool>(),
            Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var elem = callInfo.ArgAt<JsonElement>(2);
                var json = elem.ValueKind != JsonValueKind.Undefined ? elem.GetRawText() : "{}";
                var skip = callInfo.ArgAt<bool>(3);
                var version = callInfo.ArgAt<TemplateVersion>(1);
                var valResult = (!skip && !string.IsNullOrWhiteSpace(version.DataSchema))
                    ? _schemaValidation.Validate(version.DataSchema, json)
                    : null;
                return new BoundDocumentData(json, valResult);
            });
    }

    private GenerateDocumentUseCase CreateSut()
    {
        var audit = new DocumentAuditService(
            _logQueue,
            _projectContext,
            _timeProvider);

        var versioning = new DocumentVersioningService(
            _documentRepo,
            _projectContext,
            _timeProvider);

        var pipeline = new DocumentRenderPipeline(
            _engineFactory,
            _dataBindingEngine,
            _storageService,
            audit,
            _unitOfWork);

        return new GenerateDocumentUseCase(
            _templateRepo,
            _storageService,
            pipeline,
            audit,
            versioning,
            _unitOfWork,
            _projectContext,
            _timeProvider);
    }

    [Fact]
    public async Task ExecuteAsync_WhenCommandFailsValidation_ShouldThrowValidationException_WithoutExecutingServices()
    {
        // Arrange
        using var jsonDoc = JsonDocument.Parse("{}");
        var invalidCommand = new GenerateDocumentCommand("sale-contract", jsonDoc.RootElement, OutputFormat: "unsupported_format");
        var pipeline = new ValidatingUseCaseDecorator<GenerateDocumentCommand, GenerateDocumentResultDto>(
            CreateSut(),
            [new GenerateDocumentCommandValidator()]);

        // Act
        Func<Task> act = () => pipeline.ExecuteAsync(invalidCommand);

        // Assert
        await act.Should().ThrowAsync<SmkDoc.Application.Common.Exceptions.ValidationException>();
        await _templateRepo.DidNotReceive().GetBySlugWithDetailsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
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
        template.PublishVersion(version, version.CreatedAt);
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

        _documentRepo.GetByDocumentRefWithVersionsAsync(Arg.Any<DocumentReference>(), Arg.Any<CancellationToken>())
            .Returns((Document?)null);

        using var jsonDoc = JsonDocument.Parse("{\"name\": \"สมชาย ใจดี\"}");
        var request = new GenerateDocumentCommand("sale-contract", jsonDoc.RootElement, OutputFormat: "pdf", DocumentRef: "SC-2026-0001");

        // Act
        GenerateDocumentResultDto result = await CreateSut().ExecuteAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.Url.Should().StartWith("https://minio.sammakorn.co.th");
        result.OutputFormat.Should().Be("pdf");
        result.GenerationId.Version.Should().Be(7);

        await _logQueue.Received(1).EnqueueAsync(
            Arg.Is<GenerationLog>(l => l.TemplateId == templateId
                                   && l.TemplateVersionId == versionId
                                   && l.Status == "SUCCESS"
                                   && l.TriggerSource == "api"
                                   && l.CallerApp == "sales-app"),
            Arg.Any<CancellationToken>());

        await _documentRepo.Received(1).AddAsync(
            Arg.Is<Document>(d => d.DocumentRef == "SC-2026-0001" && d.Versions.Count == 1 && d.Versions.First().Version == 1),
            Arg.Any<CancellationToken>());

        await _unitOfWork.Received(1).CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenEngineReturnsNonSeekableStream_ShouldSpoolAndComputeExactSizeAndSha256()
    {
        // Arrange
        SetupActiveHtmlTemplate(slug: "non-seekable-doc");
        var payloadBytes = Encoding.UTF8.GetBytes("%PDF-1.4 Non-seekable HTTP stream payload");
        var expectedSha256 = Convert.ToHexString(SHA256.HashData(payloadBytes)).ToLowerInvariant();

        _engine.EngineType.Returns(RenderEngineType.Html);
        _engine.RenderStreamAsync(Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<OutputFormat>(), Arg.Any<CancellationToken>())
            .Returns(new NonSeekableReadOnlyStream(payloadBytes));
        SetupPresignedUrl("https://minio.sammakorn.co.th/outputs/non-seekable.pdf");

        using var jsonDoc = JsonDocument.Parse("{}");
        var request = new GenerateDocumentCommand("non-seekable-doc", jsonDoc.RootElement, OutputFormat: "pdf");

        // Act
        GenerateDocumentResultDto result = await CreateSut().ExecuteAsync(request);

        // Assert
        result.FileSizeBytes.Should().Be(payloadBytes.Length);
        result.Sha256.Should().Be(expectedSha256);
    }

    [Fact]
    public async Task ExecuteAsync_WhenRenderEngineThrows_ShouldPersistFailedGenerationLogAndRethrow()
    {
        // Arrange
        var (template, currentVersion) = SetupActiveHtmlTemplate(slug: "broken-render");
        _engine.EngineType.Returns(RenderEngineType.Html);
        _engine.RenderStreamAsync(Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<OutputFormat>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Gotenberg container crashed"));

        using var jsonDoc = JsonDocument.Parse("{\"invoice\":123}");
        var request = new GenerateDocumentCommand("broken-render", jsonDoc.RootElement, OutputFormat: "pdf");

        // Act
        Func<Task> act = () => CreateSut().ExecuteAsync(request);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("Gotenberg container crashed");
        await _logQueue.Received(1).EnqueueAsync(
            Arg.Is<GenerationLog>(l =>
                l.TemplateId == template.Id &&
                l.TemplateVersionId == currentVersion.Id &&
                l.Status == GenerationStatus.Failed &&
                l.ErrorMsg == "Gotenberg container crashed"),
            Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenPostUploadCommitFails_ShouldDeleteOrphanedOutputObject()
    {
        // Arrange
        SetupActiveHtmlTemplate(slug: "commit-fail-doc");
        SetupRenderEnginePdfOutput("%PDF-1.4");
        SetupPresignedUrl("https://minio.sammakorn.co.th/outputs/orphan.pdf");
        _unitOfWork.CommitAsync(Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("PostgreSQL deadlock"));

        using var jsonDoc = JsonDocument.Parse("{}");
        var request = new GenerateDocumentCommand("commit-fail-doc", jsonDoc.RootElement, OutputFormat: "pdf");

        // Act
        Func<Task> act = () => CreateSut().ExecuteAsync(request);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("PostgreSQL deadlock");
        await _storageService.Received(1).DeleteAsync(
            StorageBuckets.Outputs,
            Arg.Is<string>(k => k.StartsWith("outputs/") && k.EndsWith(".pdf")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenTemplateInactive_ShouldThrowNotFoundException()
    {
        // Arrange
        SetupInactiveTemplate("inactive-tpl");

        using var jsonDoc = JsonDocument.Parse("{}");
        var request = new GenerateDocumentCommand("inactive-tpl", jsonDoc.RootElement);

        // Act & Assert
        Func<Task> act = () => CreateSut().ExecuteAsync(request);
        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task ExecuteAsync_WhenNoCurrentVersion_ShouldThrowBusinessRuleViolationException()
    {
        // Arrange
        SetupTemplateWithoutVersion("no-ver-tpl");

        using var jsonDoc = JsonDocument.Parse("{}");
        var request = new GenerateDocumentCommand("no-ver-tpl", jsonDoc.RootElement);

        // Act & Assert
        Func<Task> act = () => CreateSut().ExecuteAsync(request);
        await act.Should().ThrowAsync<BusinessRuleViolationException>();
    }

    [Fact]
    public async Task ExecuteAsync_WhenPayloadProvided_ShouldPassRawPayloadDirectlyToEngineWithoutMutation()
    {
        // Arrange
        var now = TestConstants.BaselineTime;
        var (template, _) = SetupActiveHtmlTemplate(
            slug: "contract-direct",
            name: "Contract With Direct Data",
            htmlContent: "<html><body>{{contract.price}}</body></html>",
            now: now);

        string capturedDataJson = string.Empty;
        _engine.EngineType.Returns(RenderEngineType.Html);
        _engine.RenderStreamAsync(Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<OutputFormat>(), Arg.Any<CancellationToken>())
            .Returns(x =>
            {
                capturedDataJson = x.ArgAt<string>(1);
                return new MemoryStream(Encoding.UTF8.GetBytes("%PDF-1.4 Mock Output"));
            });

        SetupPresignedUrl("https://minio.sammakorn.co.th/outputs/sample.pdf");

        using var jsonDoc = JsonDocument.Parse("{\"contract\":{\"price\":\"2500000\"}}");
        var request = new GenerateDocumentCommand("contract-direct", jsonDoc.RootElement);

        // Act
        GenerateDocumentResultDto result = await CreateSut().ExecuteAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.Sha256.Should().NotBeNullOrEmpty();
        result.FileSizeBytes.Should().BeGreaterThan(0);
        capturedDataJson.Should().Be("{\"contract\":{\"price\":\"2500000\"}}");
    }

    [Fact]
    public async Task ExecuteAsync_WithExistingDocumentRef_ShouldReuseDocumentAndIncrementVersion()
    {
        // Arrange
        var now = TestConstants.BaselineTime;
        var (template, _) = SetupActiveHtmlTemplate(
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

        _documentRepo.GetByDocumentRefWithVersionsAsync(Arg.Any<DocumentReference>(), Arg.Any<CancellationToken>())
            .Returns(existingDocument);

        using var jsonDoc = JsonDocument.Parse("{}");
        var request = new GenerateDocumentCommand("sale-contract", jsonDoc.RootElement, OutputFormat: "pdf", DocumentRef: "SC-2026-0001");

        // Act
        GenerateDocumentResultDto result = await CreateSut().ExecuteAsync(request);

        // Assert
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

        var (_, currentVersion) = SetupActiveHtmlTemplate(
            slug: "invoice",
            name: "Invoice",
            now: now);

        currentVersion.UpdateDataSchema(schemaJson, null, now);

        var errors = new List<ValidationErrorItem>
        {
            new("/", "required", "Required property 'doc_no' not found in JSON.")
        };
        _schemaValidation.Validate(schemaJson, Arg.Any<string>())
            .Returns(SchemaValidationResult.Failure(errors));

        _engine.EngineType.Returns(RenderEngineType.Html);
        _storageService.DownloadAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new MemoryStream());

        _unitOfWork.CommitAsync(Arg.Any<CancellationToken>())
            .Returns(1);

        using var jsonDoc = JsonDocument.Parse("""{"customer":"ACME"}""");
        var request = new GenerateDocumentCommand("invoice", jsonDoc.RootElement, OutputFormat: "pdf");

        // Act
        var act = () => CreateSut().ExecuteAsync(request);

        // Assert
        var ex = await act.Should().ThrowAsync<SchemaValidationException>();
        ex.Which.TemplateSlug.Should().Be("invoice");
        ex.Which.Version.Should().Be(1);
        ex.Which.Errors.Should().HaveCount(1);
        ex.Which.Errors[0].Rule.Should().Be("required");

        await _logQueue.Received(1).EnqueueAsync(
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

        var (_, currentVersion) = SetupActiveHtmlTemplate(
            slug: "invoice",
            name: "Invoice",
            now: now);
        currentVersion.UpdateDataSchema(schemaJson, null, now);

        SetupRenderEnginePdfOutput();
        _storageService.UploadAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns("outputs/invoice.pdf");
        SetupPresignedUrl("https://example.com/download/invoice.pdf");

        using var jsonDoc = JsonDocument.Parse("""{"customer":"ACME"}""");
        var request = new GenerateDocumentCommand("invoice", jsonDoc.RootElement, OutputFormat: "pdf", SkipValidation: true);

        // Act
        GenerateDocumentResultDto result = await CreateSut().ExecuteAsync(request);

        // Assert
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
        var request = new GenerateDocumentCommand("fallback-contract", jsonDoc.RootElement, OutputFormat: "unknown_format", SkipValidation: true);

        // Act
        GenerateDocumentResultDto result = await CreateSut().ExecuteAsync(request);

        // Assert
        await _engine.Received(1).RenderStreamAsync(
            Arg.Any<Stream>(),
            Arg.Any<string>(),
            OutputFormat.Pdf,
            Arg.Any<CancellationToken>());
        result.Url.Should().Be("https://example.com/download/fallback.pdf");
    }

    private sealed class NonSeekableReadOnlyStream(byte[] bytes) : Stream
    {
        private readonly MemoryStream _inner = new(bytes);

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException("Stream is not seekable.");
        public override long Position
        {
            get => throw new NotSupportedException("Stream is not seekable.");
            set => throw new NotSupportedException("Stream is not seekable.");
        }

        public override void Flush() => _inner.Flush();
        public override int Read(byte[] buffer, int offset, int count) => _inner.Read(buffer, offset, count);
        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
            _inner.ReadAsync(buffer, offset, count, cancellationToken);
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
            _inner.ReadAsync(buffer, cancellationToken);
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
