using System.Linq.Expressions;
using System.Text;
using System.Text.Json;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.Authoring.FieldMappings.DTOs;
using SmkDoc.Application.Modules.Rendering.Documents;
using SmkDoc.Application.Modules.Rendering.Documents.Commands.GenerateDocument;
using SmkDoc.Application.Modules.Rendering.Documents.DTOs;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.ValueObjects.Validation;
using SmkDoc.Tests.Common.Builders;
using SmkDoc.Tests.Common.Factories;
using SmkDoc.Tests.Common.Fixtures;

namespace SmkDoc.Tests.Application.Modules.Rendering.Documents.Commands.GenerateDocument;

public class GenerateDocumentUseCaseTests
{
    private readonly GenerateDocumentTestFixture _fixture = new();

    private GenerateDocumentUseCase BuildUseCase() => _fixture.BuildUseCase();

    [Fact]
    public async Task ExecuteAsync_WhenTemplateActive_ShouldGenerateAndReturnPresignedUrl()
    {
        // Arrange
        var templateId = Guid.NewGuid();
        var versionId = Guid.NewGuid();

        var template = new TemplateBuilder()
            .WithId(templateId)
            .WithName("Sale Contract")
            .WithSlug("sale-contract")
            .WithCurrentVersion(versionId)
            .Build();

        var currentVersion = new TemplateVersionBuilder()
            .WithId(versionId)
            .WithTemplateId(templateId)
            .WithVersion(2)
            .WithStorageKey("templates/sale-contract.html")
            .WithFormat(TemplateFormat.Html)
            .Build();

        _fixture.GivenTemplateWithVersion(template, currentVersion, "<html><body>{{name}}</body></html>");
        _fixture.GivenRenderEnginePdfOutput("%PDF-1.4 Mock Output");
        _fixture.GivenPresignedUrl("https://minio.sammakorn.co.th/outputs/sample.pdf?signature=valid");

        // No existing Document for this ref
        _fixture.DocumentRepo.Setup(r => r.FirstOrDefaultAsync(It.IsAny<Expression<Func<Document, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Document?)null);

        _fixture.DocVersionRepo.Setup(r => r.MaxOrDefaultAsync(It.IsAny<Expression<Func<DocumentVersion, bool>>>(), It.IsAny<Expression<Func<DocumentVersion, int>>>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        _fixture.Context.Setup(c => c.CallerApp).Returns("sales-app");

        var useCase = BuildUseCase();
        using var jsonDoc = JsonDocument.Parse("{\"name\": \"สมชาย ใจดี\"}");
        var request = new GenerateDocumentCommand(jsonDoc.RootElement, Output: "pdf", DocumentRef: "SC-2026-0001");

        // Act
        var result = await useCase.ExecuteAsync("sale-contract", request);

        // Assert
        result.Should().NotBeNull();
        result.Url.Should().StartWith("https://minio.sammakorn.co.th");
        result.OutputFormat.Should().Be("pdf");
        result.GenerationId.Version.Should().Be(7);

        _fixture.LogRepo.Verify(r => r.AddAsync(
            It.Is<GenerationLog>(l => l.TemplateId == templateId
                                   && l.TemplateVersionId == versionId
                                   && l.Status == "SUCCESS"
                                   && l.TriggerSource == "api"),
            It.IsAny<CancellationToken>()), Times.Once);

        _fixture.DocumentRepo.Verify(r => r.AddAsync(
            It.Is<Document>(d => d.DocumentRef == "SC-2026-0001"),
            It.IsAny<CancellationToken>()), Times.Once);

        _fixture.DocVersionRepo.Verify(r => r.AddAsync(
            It.Is<DocumentVersion>(v => v.GenerationLogId != null && v.Version == 1),
            It.IsAny<CancellationToken>()), Times.Once);

        _fixture.Uow.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_WhenTemplateInactive_ShouldThrowNotFoundException()
    {
        // Arrange
        var template = new TemplateBuilder().AsInactive().WithSlug("inactive-tpl").Build();
        _fixture.TemplateRepo.Setup(r => r.GetBySlugWithDetailsAsync("inactive-tpl", It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(template);
        _fixture.TemplateRepo.Setup(r => r.GetBySlugAsync("inactive-tpl", It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(template);

        var useCase = BuildUseCase();
        using var jsonDoc = JsonDocument.Parse("{}");
        var request = new GenerateDocumentCommand(jsonDoc.RootElement);

        // Act & Assert
        Func<Task> act = () => useCase.ExecuteAsync("inactive-tpl", request);
        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task ExecuteAsync_WhenNoCurrentVersion_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var template = new TemplateBuilder().WithoutCurrentVersion().WithSlug("no-ver-tpl").Build();
        _fixture.TemplateRepo.Setup(r => r.GetBySlugWithDetailsAsync("no-ver-tpl", It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(template);
        _fixture.TemplateRepo.Setup(r => r.GetBySlugAsync("no-ver-tpl", It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(template);

        var useCase = BuildUseCase();
        using var jsonDoc = JsonDocument.Parse("{}");
        var request = new GenerateDocumentCommand(jsonDoc.RootElement);

        // Act & Assert
        Func<Task> act = () => useCase.ExecuteAsync("no-ver-tpl", request);
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task ExecuteAsync_WithFieldMappings_ShouldTransformAndMergeData()
    {
        // Arrange
        var templateId = Guid.NewGuid();
        var versionId = Guid.NewGuid();
        var now = TestConstants.BaselineTime;

        var template = new TemplateBuilder()
            .WithId(templateId)
            .WithName("Contract With Mappings")
            .WithSlug("contract-mapped")
            .WithCurrentVersion(versionId)
            .WithTime(now)
            .Build();

        var currentVersion = new TemplateVersionBuilder()
            .WithId(versionId)
            .WithTemplateId(templateId)
            .WithVersion(1)
            .WithStorageKey("templates/contract-mapped.html")
            .WithFormat(TemplateFormat.Html)
            .WithTime(now)
            .Build();

        _fixture.GivenTemplateWithVersion(template, currentVersion, "<html><body>{{amount_baht}}</body></html>");

        var mappings = new List<FieldMapping>
        {
            FieldMapping.Create(templateId, "amount_baht", "contract.price", "Price in Baht Text", true, 1, now, DataSourceType.Json)
        };

        mappings[0].UpdateMappingDetails("contract.price", "Price in Baht Text", true, null, "baht", 1, now);
        template.ReplaceFieldMappings(mappings, now);

        _fixture.Applicator.Setup(s => s.ApplyAsync(
                It.IsAny<JsonElement>(),
                It.IsAny<IEnumerable<FieldMapping>>(),
                It.IsAny<IReadOnlyDictionary<string, ResolvedDatasetContext>>()))
            .ReturnsAsync("{\"amount_baht\":\"สองล้านห้าแสนบาทถ้วน\"}");

        string capturedDataJson = string.Empty;
        _fixture.Engine.Setup(e => e.EngineType).Returns(RenderEngineType.Html);
        _fixture.Engine.Setup(e => e.RenderStreamAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<OutputFormat>(), It.IsAny<CancellationToken>()))
            .Callback<Stream, string, OutputFormat, CancellationToken>((_, json, _, _) => capturedDataJson = json)
            .ReturnsAsync(new MemoryStream(Encoding.UTF8.GetBytes("%PDF-1.4 Mock Output")));

        _fixture.GivenPresignedUrl("https://minio.sammakorn.co.th/outputs/sample.pdf");

        var useCase = BuildUseCase();
        using var jsonDoc = JsonDocument.Parse("{\"contract\": {\"price\": \"2500000\"}}");
        var request = new GenerateDocumentCommand(jsonDoc.RootElement);

        // Act
        var result = await useCase.ExecuteAsync("contract-mapped", request);

        // Assert
        result.Should().NotBeNull();
        capturedDataJson.Should().Contain("สองล้านห้าแสนบาทถ้วน");
        capturedDataJson.Should().Contain("amount_baht");
    }

    [Fact]
    public async Task ExecuteAsync_WithExistingDocumentRef_ShouldReuseDocumentAndIncrementVersion()
    {
        // Arrange
        var templateId = Guid.NewGuid();
        var versionId = Guid.NewGuid();
        var documentId = Guid.NewGuid();
        var now = TestConstants.BaselineTime;

        var template = new TemplateBuilder()
            .WithId(templateId)
            .WithName("Contract")
            .WithSlug("sale-contract")
            .WithCurrentVersion(versionId)
            .WithTime(now)
            .Build();

        var currentVersion = new TemplateVersionBuilder()
            .WithId(versionId)
            .WithTemplateId(templateId)
            .WithVersion(1)
            .WithStorageKey("templates/sale-contract.html")
            .WithFormat(TemplateFormat.Html)
            .WithTime(now)
            .Build();

        var existingDocument = new DocumentBuilder()
            .WithId(documentId)
            .WithDocumentRef("SC-2026-0001")
            .WithTemplateId(templateId)
            .WithTime(now)
            .Build();

        _fixture.GivenTemplateWithVersion(template, currentVersion);
        _fixture.GivenRenderEnginePdfOutput("%PDF-1.4");
        _fixture.GivenPresignedUrl("https://minio.sammakorn.co.th/outputs/sc.pdf");

        // Existing document found
        _fixture.DocumentRepo.Setup(r => r.FirstOrDefaultAsync(It.IsAny<Expression<Func<Document, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingDocument);

        // Current max version is 2 — next will be 3
        _fixture.DocVersionRepo.Setup(r => r.MaxOrDefaultAsync(It.IsAny<Expression<Func<DocumentVersion, bool>>>(), It.IsAny<Expression<Func<DocumentVersion, int>>>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(2);

        var useCase = BuildUseCase();
        using var jsonDoc = JsonDocument.Parse("{}");
        var request = new GenerateDocumentCommand(jsonDoc.RootElement, Output: "pdf", DocumentRef: "SC-2026-0001");

        // Act
        await useCase.ExecuteAsync("sale-contract", request);

        // Assert — Document NOT created again
        _fixture.DocumentRepo.Verify(r => r.AddAsync(It.IsAny<Document>(), It.IsAny<CancellationToken>()), Times.Never);

        _fixture.DocVersionRepo.Verify(r => r.AddAsync(
            It.Is<DocumentVersion>(v => v.DocumentId == documentId && v.Version == 3),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Schema Validation Gate Tests
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ExecuteAsync_WhenPayloadFailsSchema_ShouldThrowSchemaValidationExceptionAndLogFailed()
    {
        // Arrange
        var templateId = Guid.NewGuid();
        var versionId = Guid.NewGuid();
        const string schemaJson = """{"$schema":"http://json-schema.org/draft-07/schema#","type":"object","required":["doc_no"]}""";
        var now = TestConstants.BaselineTime;

        var template = new TemplateBuilder()
            .WithId(templateId)
            .WithName("Invoice")
            .WithSlug("invoice")
            .WithCurrentVersion(versionId)
            .WithTime(now)
            .Build();

        var currentVersion = new TemplateVersionBuilder()
            .WithId(versionId)
            .WithTemplateId(templateId)
            .WithVersion(1)
            .WithStorageKey("templates/invoice.html")
            .WithFormat(TemplateFormat.Html)
            .WithTime(now)
            .Build();

        currentVersion.UpdateDataSchema(schemaJson, null, now);

        _fixture.TemplateRepo.Setup(r => r.GetBySlugWithDetailsAsync("invoice", It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(template);
        _fixture.TemplateRepo.Setup(r => r.GetBySlugAsync("invoice", It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(template);
        _fixture.VersionRepo.Setup(r => r.GetByIdAsync(versionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(currentVersion);

        // Service returns invalid result (missing required field "doc_no")
        var errors = new List<ValidationErrorItem>
        {
            new("/", "required", "Required property 'doc_no' not found in JSON.")
        };
        _fixture.SchemaValidation
            .Setup(s => s.Validate(schemaJson, It.IsAny<string>()))
            .Returns(SchemaValidationResult.Failure(errors));

        // Engine and storage must be set up so the use case reaches the validation gate
        _fixture.Engine.Setup(e => e.EngineType).Returns(RenderEngineType.Html);
        _fixture.Storage.Setup(s => s.DownloadAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MemoryStream());

        _fixture.LogRepo.Setup(r => r.AddAsync(It.IsAny<GenerationLog>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _fixture.Uow.Setup(u => u.CommitAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var useCase = BuildUseCase();
        using var jsonDoc = JsonDocument.Parse("""{"customer":"ACME"}"""); // missing doc_no
        var request = new GenerateDocumentCommand(jsonDoc.RootElement, Output: "pdf");

        // Act
        var act = async () => await useCase.ExecuteAsync("invoice", request);

        // Assert — SchemaValidationException is thrown
        var ex = await act.Should().ThrowAsync<SchemaValidationException>();
        ex.Which.TemplateSlug.Should().Be("invoice");
        ex.Which.Version.Should().Be(1);
        ex.Which.Errors.Should().HaveCount(1);
        ex.Which.Errors[0].Rule.Should().Be("required");

        // Assert — VALIDATION_FAILED logged (no rendering attempted)
        _fixture.LogRepo.Verify(r => r.AddAsync(
            It.Is<GenerationLog>(l => l.Status == "VALIDATION_FAILED"),
            It.IsAny<CancellationToken>()), Times.Once);
        _fixture.Engine.Verify(e => e.RenderStreamAsync(
            It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<OutputFormat>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _fixture.Storage.Verify(s => s.DownloadAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_WhenSkipValidationTrue_ShouldBypassSchemaAndRender()
    {
        // Arrange
        var templateId = Guid.NewGuid();
        var versionId = Guid.NewGuid();
        const string schemaJson = """{"$schema":"http://json-schema.org/draft-07/schema#","type":"object","required":["doc_no"]}""";
        var now = TestConstants.BaselineTime;

        var template = new TemplateBuilder()
            .WithId(templateId)
            .WithName("Invoice")
            .WithSlug("invoice")
            .WithCurrentVersion(versionId)
            .WithTime(now)
            .Build();

        var currentVersion = new TemplateVersionBuilder()
            .WithId(versionId)
            .WithTemplateId(templateId)
            .WithVersion(1)
            .WithStorageKey("templates/invoice.html")
            .WithFormat(TemplateFormat.Html)
            .WithTime(now)
            .Build();

        currentVersion.UpdateDataSchema(schemaJson, null, now);

        _fixture.TemplateRepo.Setup(r => r.GetBySlugWithDetailsAsync("invoice", It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(template);
        _fixture.TemplateRepo.Setup(r => r.GetBySlugAsync("invoice", It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(template);
        _fixture.VersionRepo.Setup(r => r.GetByIdAsync(versionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(currentVersion);

        _fixture.Engine.Setup(e => e.EngineType).Returns(RenderEngineType.Html);
        _fixture.Engine.Setup(e => e.RenderStreamAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<OutputFormat>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MemoryStream(new byte[] { 1, 2, 3 }));
        _fixture.Storage.Setup(s => s.DownloadAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MemoryStream());
        _fixture.Storage.Setup(s => s.UploadAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("outputs/invoice.pdf");
        _fixture.GivenPresignedUrl("https://example.com/download/invoice.pdf");
        _fixture.LogRepo.Setup(r => r.AddAsync(It.IsAny<GenerationLog>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _fixture.Uow.Setup(u => u.CommitAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var useCase = BuildUseCase();
        using var jsonDoc = JsonDocument.Parse("""{"customer":"ACME"}"""); // missing doc_no — but SkipValidation
        var request = new GenerateDocumentCommand(jsonDoc.RootElement, Output: "pdf", SkipValidation: true);

        // Act
        var result = await useCase.ExecuteAsync("invoice", request);

        // Assert — validation service never called; render completed successfully
        _fixture.SchemaValidation.Verify(s => s.Validate(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        _fixture.Engine.Verify(e => e.RenderStreamAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<OutputFormat>(), It.IsAny<CancellationToken>()), Times.Once);
        result.Url.Should().Be("https://example.com/download/invoice.pdf");
    }
}
