using System.Linq.Expressions;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Moq;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Common.Models;
using SmkDoc.Application.Engines;
using SmkDoc.Application.UseCases.Documents;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Enums;
using SmkDoc.Domain.Exceptions;
using Xunit;

namespace SmkDoc.Tests;

public class GenerateDocumentUseCaseTests
{
    private readonly Mock<IRepository<Template>>        _mockTemplateRepo    = new();
    private readonly Mock<IRepository<TemplateVersion>> _mockVersionRepo     = new();
    private readonly Mock<IRepository<FieldMapping>>    _mockMappingRepo     = new();
    private readonly Mock<IRepository<TemplateDataset>> _mockTdRepo          = new();
    private readonly Mock<IRepository<Dataset>>         _mockDatasetRepo     = new();
    private readonly Mock<IRepository<DataConnection>>  _mockConnectionRepo  = new();
    private readonly Mock<IRepository<GenerationLog>>   _mockLogRepo         = new();
    private readonly Mock<IRepository<Document>>        _mockDocumentRepo    = new();
    private readonly Mock<IRepository<DocumentVersion>> _mockDocVersionRepo  = new();
    private readonly Mock<IStorageService>              _mockStorage         = new();
    private readonly Mock<IRenderEngine>                _mockEngine          = new();
    private readonly Mock<IExecutionContext>             _mockContext         = new();
    private readonly Mock<IUnitOfWork>                  _mockUow             = new();
    private readonly Mock<IFieldMappingApplicatorService> _mockApplicator    = new();
    private readonly Mock<IDataProtectionService>       _mockDataProtection  = new();
    private readonly Mock<IJsonSchemaValidationService> _mockSchemaValidation = new();

    private GenerateDocumentUseCase BuildUseCase() => new(
        _mockTemplateRepo.Object,
        _mockVersionRepo.Object,
        _mockMappingRepo.Object,
        _mockTdRepo.Object,
        _mockDatasetRepo.Object,
        _mockConnectionRepo.Object,
        _mockLogRepo.Object,
        _mockDocumentRepo.Object,
        _mockDocVersionRepo.Object,
        _mockStorage.Object,
        new[] { _mockEngine.Object },
        _mockContext.Object,
        _mockUow.Object,
        _mockApplicator.Object,
        _mockDataProtection.Object,
        _mockSchemaValidation.Object
    );

    [Fact]
    public async Task ExecuteAsync_WhenTemplateActive_ShouldGenerateAndReturnPresignedUrl()
    {
        // Arrange
        var templateId = Guid.NewGuid();
        var versionId  = Guid.NewGuid();

        var template = new Template
        {
            Id = templateId,
            Name = "Sale Contract",
            Slug = "sale-contract",
            IsActive = true,
            CurrentVersionId = versionId
        };

        var currentVersion = new TemplateVersion
        {
            Id = versionId,
            TemplateId = templateId,
            Version = 2,
            StorageKey = "templates/sale-contract.html"
        };

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
        var request = new GenerateDocumentRequest(jsonDoc.RootElement, Output: "pdf", DocumentRef: "SC-2026-0001");

        // Act
        var result = await useCase.ExecuteAsync("sale-contract", request);

        // Assert
        result.Should().NotBeNull();
        result.Url.Should().StartWith("https://minio.sammakorn.co.th");
        result.OutputFormat.Should().Be("pdf");

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

        _mockUow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_WhenTemplateInactive_ShouldThrowKeyNotFoundException()
    {
        // Arrange
        var template = new Template { Slug = "inactive-tpl", IsActive = false };
        _mockTemplateRepo.Setup(r => r.FirstOrDefaultAsync(It.IsAny<Expression<Func<Template, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(template);

        var useCase = BuildUseCase();
        using var jsonDoc = JsonDocument.Parse("{}");
        var request = new GenerateDocumentRequest(jsonDoc.RootElement);

        // Act & Assert
        await Assert.ThrowsAsync<KeyNotFoundException>(() => useCase.ExecuteAsync("inactive-tpl", request));
    }

    [Fact]
    public async Task ExecuteAsync_WhenNoCurrentVersion_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var template = new Template { Slug = "no-ver-tpl", IsActive = true, CurrentVersionId = null };
        _mockTemplateRepo.Setup(r => r.FirstOrDefaultAsync(It.IsAny<Expression<Func<Template, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(template);

        var useCase = BuildUseCase();
        using var jsonDoc = JsonDocument.Parse("{}");
        var request = new GenerateDocumentRequest(jsonDoc.RootElement);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => useCase.ExecuteAsync("no-ver-tpl", request));
    }

    [Fact]
    public async Task ExecuteAsync_WithFieldMappings_ShouldTransformAndMergeData()
    {
        // Arrange
        var templateId = Guid.NewGuid();
        var versionId  = Guid.NewGuid();

        var template = new Template
        {
            Id = templateId,
            Name = "Contract With Mappings",
            Slug = "contract-mapped",
            IsActive = true,
            CurrentVersionId = versionId
        };

        var currentVersion = new TemplateVersion
        {
            Id = versionId,
            TemplateId = templateId,
            Version = 1,
            StorageKey = "templates/contract-mapped.html"
        };

        _mockTemplateRepo.Setup(r => r.FirstOrDefaultAsync(It.IsAny<Expression<Func<Template, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(template);

        _mockVersionRepo.Setup(r => r.GetByIdAsync(versionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(currentVersion);

        _mockStorage.Setup(s => s.DownloadAsync("templates", currentVersion.StorageKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MemoryStream(Encoding.UTF8.GetBytes("<html><body>{{amount_baht}}</body></html>")));

        var mappings = new List<FieldMapping>
        {
            new() { Id = Guid.NewGuid(), TemplateId = templateId,
                    Placeholder = "amount_baht", SourcePath = "contract.price",
                    Label = "Price in Baht Text", Transform = "baht", Required = true }
        };

        _mockMappingRepo.Setup(r => r.ListAsync(It.IsAny<Expression<Func<FieldMapping, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(mappings);

        _mockTdRepo.Setup(r => r.ListAsync(It.IsAny<Expression<Func<TemplateDataset, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        _mockApplicator.Setup(s => s.ApplyAsync(
                It.IsAny<JsonElement>(),
                It.IsAny<IEnumerable<FieldMapping>>(),
                It.IsAny<IReadOnlyDictionary<string, ResolvedDataset>>()))
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
        var request = new GenerateDocumentRequest(jsonDoc.RootElement);

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

        var template = new Template
        {
            Id = templateId, Slug = "sale-contract",
            IsActive = true, CurrentVersionId = versionId
        };
        var currentVersion = new TemplateVersion
        {
            Id = versionId, TemplateId = templateId,
            Version = 1, StorageKey = "templates/sale-contract.html"
        };
        var existingDocument = new Document
        {
            Id = documentId, DocumentRef = "SC-2026-0001", TemplateId = templateId
        };

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
        var request = new GenerateDocumentRequest(jsonDoc.RootElement, Output: "pdf", DocumentRef: "SC-2026-0001");

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

        var template = new Template
        {
            Id = templateId, Name = "Invoice", Slug = "invoice",
            IsActive = true, CurrentVersionId = versionId
        };
        var currentVersion = new TemplateVersion
        {
            Id = versionId, TemplateId = templateId, Version = 1,
            StorageKey = "templates/invoice.html",
            DataSchema = schemaJson          // Schema requires "doc_no"
        };

        _mockTemplateRepo.Setup(r => r.FirstOrDefaultAsync(It.IsAny<Expression<Func<Template, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(template);
        _mockVersionRepo.Setup(r => r.GetByIdAsync(versionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(currentVersion);
        _mockMappingRepo.Setup(r => r.ListAsync(It.IsAny<Expression<Func<FieldMapping, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        // Service returns invalid result (missing required field "doc_no")
        var errors = new List<SchemaValidationError>
        {
            new("/", "Required property 'doc_no' not found in JSON.", "required")
        };
        _mockSchemaValidation
            .Setup(s => s.Validate(schemaJson, It.IsAny<string>()))
            .Returns(new SchemaValidationResult(IsValid: false, Errors: errors));

        // Engine and storage must be set up so the use case reaches the validation gate
        _mockEngine.Setup(e => e.EngineType).Returns(RenderEngineType.Html);
        _mockStorage.Setup(s => s.DownloadAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MemoryStream());

        _mockLogRepo.Setup(r => r.AddAsync(It.IsAny<GenerationLog>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _mockUow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var useCase = BuildUseCase();
        using var jsonDoc = JsonDocument.Parse("""{"customer":"ACME"}"""); // missing doc_no
        var request = new GenerateDocumentRequest(jsonDoc.RootElement, Output: "pdf");

        // Act
        var act = async () => await useCase.ExecuteAsync("invoice", request);

        // Assert — SchemaValidationException is thrown
        var ex = await act.Should().ThrowAsync<SchemaValidationException>();
        ex.Which.TemplateSlug.Should().Be("invoice");
        ex.Which.Version.Should().Be(1);
        ex.Which.Errors.Should().HaveCount(1);
        ex.Which.Errors[0].SchemaRule.Should().Be("required");

        // Assert — VALIDATION_FAILED logged (no rendering attempted)
        _mockLogRepo.Verify(r => r.AddAsync(
            It.Is<GenerationLog>(l => l.Status == "VALIDATION_FAILED"),
            It.IsAny<CancellationToken>()), Times.Once);
        _mockEngine.Verify(e => e.RenderAsync(
            It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<OutputFormat>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_WhenSkipValidationTrue_ShouldBypassSchemaAndRender()
    {
        // Arrange
        var templateId = Guid.NewGuid();
        var versionId  = Guid.NewGuid();
        const string schemaJson = """{"$schema":"http://json-schema.org/draft-07/schema#","type":"object","required":["doc_no"]}""";

        var template = new Template
        {
            Id = templateId, Name = "Invoice", Slug = "invoice",
            IsActive = true, CurrentVersionId = versionId
        };
        var currentVersion = new TemplateVersion
        {
            Id = versionId, TemplateId = templateId, Version = 1,
            StorageKey = "templates/invoice.html",
            DataSchema = schemaJson
        };

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
        _mockUow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var useCase = BuildUseCase();
        using var jsonDoc = JsonDocument.Parse("""{"customer":"ACME"}"""); // missing doc_no — but SkipValidation
        var request = new GenerateDocumentRequest(jsonDoc.RootElement, Output: "pdf", SkipValidation: true);

        // Act
        var result = await useCase.ExecuteAsync("invoice", request);

        // Assert — validation service never called; render completed successfully
        _mockSchemaValidation.Verify(s => s.Validate(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        _mockEngine.Verify(e => e.RenderAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<OutputFormat>(), It.IsAny<CancellationToken>()), Times.Once);
        result.Url.Should().Be("https://example.com/download/invoice.pdf");
    }
}

