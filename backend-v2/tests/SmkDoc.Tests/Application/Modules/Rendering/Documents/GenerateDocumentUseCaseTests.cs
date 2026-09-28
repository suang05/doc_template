using System.Linq.Expressions;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Moq;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.Rendering.Documents.DTOs;
using SmkDoc.Application.Modules.Authoring.FieldMappings.DTOs;
using SmkDoc.Application.Modules.Rendering.Documents;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Enums;
using SmkDoc.Domain.Exceptions;
using SmkDoc.Domain.ValueObjects;
using Xunit;
using SmkDoc.Domain.Interfaces;
using SmkDoc.Domain.ValueObjects.Validation;
using SmkDoc.Tests.Common.Builders;
using SmkDoc.Tests.Common.Fixtures;

namespace SmkDoc.Tests.Application.Modules.Rendering.Documents;

public class GenerateDocumentUseCaseTests
{
    private readonly GenerateDocumentTestFixture _fixture = new();

    private Mock<IRepository<Template>> _mockTemplateRepo => _fixture.TemplateRepo;
    private Mock<IRepository<TemplateVersion>> _mockVersionRepo => _fixture.VersionRepo;
    private Mock<IRepository<FieldMapping>> _mockMappingRepo => _fixture.MappingRepo;
    private Mock<IRepository<TemplateDataset>> _mockTdRepo => _fixture.TdRepo;
    private Mock<IRepository<Dataset>> _mockDatasetRepo => _fixture.DatasetRepo;
    private Mock<IRepository<DataConnection>> _mockConnectionRepo => _fixture.ConnectionRepo;
    private Mock<IRepository<GenerationLog>> _mockLogRepo => _fixture.LogRepo;
    private Mock<IRepository<Document>> _mockDocumentRepo => _fixture.DocumentRepo;
    private Mock<IRepository<DocumentVersion>> _mockDocVersionRepo => _fixture.DocVersionRepo;
    private Mock<IStorageService> _mockStorage => _fixture.Storage;
    private Mock<IRenderEngine> _mockEngine => _fixture.Engine;
    private Mock<IExecutionContext> _mockContext => _fixture.Context;
    private Mock<IUnitOfWork> _mockUow => _fixture.Uow;
    private Mock<IFieldMappingApplicatorService> _mockApplicator => _fixture.Applicator;
    private Mock<IDataProtectionService> _mockDataProtection => _fixture.DataProtection;
    private Mock<IJsonSchemaValidationService> _mockSchemaValidation => _fixture.SchemaValidation;

    private GenerateDocumentUseCase BuildUseCase() => _fixture.BuildUseCase();

    [Fact]
    public async Task ExecuteAsync_WhenTemplateActive_ShouldGenerateAndReturnPresignedUrl()
    {
        // Arrange
        var templateId = Guid.NewGuid();
        var versionId  = Guid.NewGuid();

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

        _mockTemplateRepo.Setup(r => r.FirstOrDefaultAsync(It.IsAny<Expression<Func<Template, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(template);

        _mockVersionRepo.Setup(r => r.GetByIdAsync(versionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(currentVersion);

        _mockStorage.Setup(s => s.DownloadAsync("templates", currentVersion.StorageKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MemoryStream(Encoding.UTF8.GetBytes("<html><body>{{name}}</body></html>")));

        _mockEngine.Setup(e => e.EngineType).Returns(RenderEngineType.Html);
        _mockEngine.Setup(e => e.RenderAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<OutputFormat>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Encoding.UTF8.GetBytes("%PDF-1.4 Mock Output"));

        _mockMappingRepo.Setup(r => r.ListAsync(It.IsAny<Expression<Func<FieldMapping, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        // No existing Document for this ref
        _mockDocumentRepo.Setup(r => r.FirstOrDefaultAsync(It.IsAny<Expression<Func<Document, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Document?)null);

        _mockDocVersionRepo.Setup(r => r.MaxOrDefaultAsync(It.IsAny<Expression<Func<DocumentVersion, bool>>>(), It.IsAny<Expression<Func<DocumentVersion, int>>>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        _mockStorage.Setup(s => s.GetPresignedUrlAsync("outputs", It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("https://minio.sammakorn.co.th/outputs/sample.pdf?signature=valid");

        _mockContext.Setup(c => c.CallerApp).Returns("sales-app");

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

        _mockLogRepo.Verify(r => r.AddAsync(
            It.Is<GenerationLog>(l => l.TemplateId == templateId
                                   && l.TemplateVersionId == versionId
                                   && l.Status == "SUCCESS"
                                   && l.TriggerSource == "api"),
            It.IsAny<CancellationToken>()), Times.Once);

        _mockDocumentRepo.Verify(r => r.AddAsync(
            It.Is<Document>(d => d.DocumentRef == "SC-2026-0001"),
            It.IsAny<CancellationToken>()), Times.Once);

        _mockDocVersionRepo.Verify(r => r.AddAsync(
            It.Is<DocumentVersion>(v => v.GenerationLogId != null && v.Version == 1),
            It.IsAny<CancellationToken>()), Times.Once);

        _mockUow.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_WhenTemplateInactive_ShouldThrowNotFoundException()
    {
        // Arrange
        var template = new TemplateBuilder().AsInactive().WithSlug("inactive-tpl").Build();
        _mockTemplateRepo.Setup(r => r.FirstOrDefaultAsync(It.IsAny<Expression<Func<Template, bool>>>(), It.IsAny<CancellationToken>()))
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
        _mockTemplateRepo.Setup(r => r.FirstOrDefaultAsync(It.IsAny<Expression<Func<Template, bool>>>(), It.IsAny<CancellationToken>()))
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
        var versionId  = Guid.NewGuid();

        var template = new Template(Guid.NewGuid(), "Contract With Mappings", "contract-mapped", null) { Id = templateId };
        template.SetCurrentVersion(versionId);

        var currentVersion = new TemplateVersion(templateId, 1, "templates/contract-mapped.html", TemplateFormat.Html, "Published", "Commit") { Id = versionId };

        _mockTemplateRepo.Setup(r => r.FirstOrDefaultAsync(It.IsAny<Expression<Func<Template, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(template);

        _mockVersionRepo.Setup(r => r.GetByIdAsync(versionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(currentVersion);

        _mockStorage.Setup(s => s.DownloadAsync("templates", currentVersion.StorageKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MemoryStream(Encoding.UTF8.GetBytes("<html><body>{{amount_baht}}</body></html>")));

        var mappings = new List<FieldMapping>
        {
            new FieldMapping(templateId, "amount_baht", "contract.price", "Price in Baht Text", true, 1, DataSourceType.Json) { Id = Guid.NewGuid() }
        };

        mappings[0].UpdateMappingDetails("contract.price", "Price in Baht Text", true, null, "baht", 1);
        _mockMappingRepo.Setup(r => r.ListAsync(It.IsAny<Expression<Func<FieldMapping, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(mappings);

        _mockTdRepo.Setup(r => r.ListAsync(It.IsAny<Expression<Func<TemplateDataset, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        _mockApplicator.Setup(s => s.ApplyAsync(
                It.IsAny<JsonElement>(),
                It.IsAny<IEnumerable<FieldMapping>>(),
                It.IsAny<IReadOnlyDictionary<string, ResolvedDatasetContext>>()))
            .ReturnsAsync("{\"amount_baht\":\"สองล้านห้าแสนบาทถ้วน\"}");

        string capturedDataJson = string.Empty;
        _mockEngine.Setup(e => e.EngineType).Returns(RenderEngineType.Html);
        _mockEngine.Setup(e => e.RenderAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<OutputFormat>(), It.IsAny<CancellationToken>()))
            .Callback<Stream, string, OutputFormat, CancellationToken>((_, json, _, _) => capturedDataJson = json)
            .ReturnsAsync(Encoding.UTF8.GetBytes("%PDF-1.4 Mock Output"));

        _mockStorage.Setup(s => s.GetPresignedUrlAsync("outputs", It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("https://minio.sammakorn.co.th/outputs/sample.pdf");

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
        var templateId  = Guid.NewGuid();
        var versionId   = Guid.NewGuid();
        var documentId  = Guid.NewGuid();

        var template = new Template(Guid.NewGuid(), "Contract", "sale-contract", null) { Id = templateId };
        template.SetCurrentVersion(versionId);
        var currentVersion = new TemplateVersion(templateId, 1, "templates/sale-contract.html", TemplateFormat.Html, "Published", "Commit") { Id = versionId };
        var existingDocument = new Document("SC-2026-0001", templateId) { Id = documentId };

        _mockTemplateRepo.Setup(r => r.FirstOrDefaultAsync(It.IsAny<Expression<Func<Template, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(template);
        _mockVersionRepo.Setup(r => r.GetByIdAsync(versionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(currentVersion);
        _mockStorage.Setup(s => s.DownloadAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MemoryStream(Encoding.UTF8.GetBytes("<html></html>")));
        _mockEngine.Setup(e => e.EngineType).Returns(RenderEngineType.Html);
        _mockEngine.Setup(e => e.RenderAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<OutputFormat>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Encoding.UTF8.GetBytes("%PDF-1.4"));
        _mockStorage.Setup(s => s.GetPresignedUrlAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("https://minio.sammakorn.co.th/outputs/sc.pdf");

        _mockMappingRepo.Setup(r => r.ListAsync(It.IsAny<Expression<Func<FieldMapping, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        // Existing document found
        _mockDocumentRepo.Setup(r => r.FirstOrDefaultAsync(It.IsAny<Expression<Func<Document, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingDocument);

        // Current max version is 2 — next will be 3
        _mockDocVersionRepo.Setup(r => r.MaxOrDefaultAsync(It.IsAny<Expression<Func<DocumentVersion, bool>>>(), It.IsAny<Expression<Func<DocumentVersion, int>>>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(2);

        var useCase = BuildUseCase();
        using var jsonDoc = JsonDocument.Parse("{}");
        var request = new GenerateDocumentCommand(jsonDoc.RootElement, Output: "pdf", DocumentRef: "SC-2026-0001");

        // Act
        await useCase.ExecuteAsync("sale-contract", request);

        // Assert — Document NOT created again
        _mockDocumentRepo.Verify(r => r.AddAsync(It.IsAny<Document>(), It.IsAny<CancellationToken>()), Times.Never);

        _mockDocVersionRepo.Verify(r => r.AddAsync(
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
        var versionId  = Guid.NewGuid();
        const string schemaJson = """{"$schema":"http://json-schema.org/draft-07/schema#","type":"object","required":["doc_no"]}""";

        var template = new Template(Guid.NewGuid(), "Invoice", "invoice", null) { Id = templateId };
        template.SetCurrentVersion(versionId);
        var currentVersion = new TemplateVersion(templateId, 1, "templates/invoice.html", TemplateFormat.Html, "Published", "Commit") { Id = versionId };
        currentVersion.UpdateDataSchema(schemaJson, null);

        _mockTemplateRepo.Setup(r => r.FirstOrDefaultAsync(It.IsAny<Expression<Func<Template, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(template);
        _mockVersionRepo.Setup(r => r.GetByIdAsync(versionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(currentVersion);
        _mockMappingRepo.Setup(r => r.ListAsync(It.IsAny<Expression<Func<FieldMapping, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        // Service returns invalid result (missing required field "doc_no")
        var errors = new List<ValidationErrorItem>
        {
            new("/", "required", "Required property 'doc_no' not found in JSON.")
        };
        _mockSchemaValidation
            .Setup(s => s.Validate(schemaJson, It.IsAny<string>()))
            .Returns(SchemaValidationResult.Failure(errors));

        // Engine and storage must be set up so the use case reaches the validation gate
        _mockEngine.Setup(e => e.EngineType).Returns(RenderEngineType.Html);
        _mockStorage.Setup(s => s.DownloadAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MemoryStream());

        _mockLogRepo.Setup(r => r.AddAsync(It.IsAny<GenerationLog>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _mockUow.Setup(u => u.CommitAsync(It.IsAny<CancellationToken>()))
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
        _mockLogRepo.Verify(r => r.AddAsync(
            It.Is<GenerationLog>(l => l.Status == "VALIDATION_FAILED"),
            It.IsAny<CancellationToken>()), Times.Once);
        _mockEngine.Verify(e => e.RenderAsync(
            It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<OutputFormat>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _mockStorage.Verify(s => s.DownloadAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_WhenSkipValidationTrue_ShouldBypassSchemaAndRender()
    {
        // Arrange
        var templateId = Guid.NewGuid();
        var versionId  = Guid.NewGuid();
        const string schemaJson = """{"$schema":"http://json-schema.org/draft-07/schema#","type":"object","required":["doc_no"]}""";

        var template = new Template(Guid.NewGuid(), "Invoice", "invoice", null) { Id = templateId };
        template.SetCurrentVersion(versionId);
        var currentVersion = new TemplateVersion(templateId, 1, "templates/invoice.html", TemplateFormat.Html, "Published", "Commit") { Id = versionId };
        currentVersion.UpdateDataSchema(schemaJson, null);

        _mockTemplateRepo.Setup(r => r.FirstOrDefaultAsync(It.IsAny<Expression<Func<Template, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(template);
        _mockVersionRepo.Setup(r => r.GetByIdAsync(versionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(currentVersion);
        _mockMappingRepo.Setup(r => r.ListAsync(It.IsAny<Expression<Func<FieldMapping, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        _mockEngine.Setup(e => e.EngineType).Returns(RenderEngineType.Html);
        _mockEngine.Setup(e => e.RenderAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<OutputFormat>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new byte[] { 1, 2, 3 });
        _mockStorage.Setup(s => s.DownloadAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MemoryStream());
        _mockStorage.Setup(s => s.UploadAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("outputs/invoice.pdf");
        _mockStorage.Setup(s => s.GetPresignedUrlAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("https://example.com/download/invoice.pdf");
        _mockLogRepo.Setup(r => r.AddAsync(It.IsAny<GenerationLog>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _mockUow.Setup(u => u.CommitAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var useCase = BuildUseCase();
        using var jsonDoc = JsonDocument.Parse("""{"customer":"ACME"}"""); // missing doc_no — but SkipValidation
        var request = new GenerateDocumentCommand(jsonDoc.RootElement, Output: "pdf", SkipValidation: true);

        // Act
        var result = await useCase.ExecuteAsync("invoice", request);

        // Assert — validation service never called; render completed successfully
        _mockSchemaValidation.Verify(s => s.Validate(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        _mockEngine.Verify(e => e.RenderAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<OutputFormat>(), It.IsAny<CancellationToken>()), Times.Once);
        result.Url.Should().Be("https://example.com/download/invoice.pdf");
    }
}

