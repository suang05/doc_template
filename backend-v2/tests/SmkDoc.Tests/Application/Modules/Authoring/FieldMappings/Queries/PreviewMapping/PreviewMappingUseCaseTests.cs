using System.Text.Json;
using SmkDoc.Application.Common;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.Authoring.FieldMappings.DTOs;
using SmkDoc.Application.Modules.Authoring.FieldMappings.Queries.PreviewMapping;
using SmkDoc.Domain.Entities;
using SmkDoc.Tests.Common.Builders;

namespace SmkDoc.Tests.Application.Modules.Authoring.FieldMappings.Queries.PreviewMapping;

public class PreviewMappingUseCaseTests
{
    private readonly Mock<ITemplateRepository> _mockTemplateRepo = new();
    private readonly Mock<IDatasetRepository> _mockDatasetRepo = new();
    private readonly Mock<IDataConnectionRepository> _mockConnectionRepo = new();
    private readonly Mock<IStorageService> _mockStorage = new();
    private readonly Mock<IRenderEngine> _mockHtmlEngine = new();
    private readonly Mock<IFieldMappingApplicatorService> _mockApplicator = new();
    private readonly Mock<IDataProtectionService> _mockDataProtection = new();

    public PreviewMappingUseCaseTests()
    {
        _mockHtmlEngine.Setup(e => e.EngineType).Returns(RenderEngineType.Html);
    }

    private PreviewMappingUseCase CreateSut() =>
        new(
            _mockTemplateRepo.Object,
            _mockDatasetRepo.Object,
            _mockConnectionRepo.Object,
            _mockStorage.Object,
            [_mockHtmlEngine.Object],
            _mockApplicator.Object,
            _mockDataProtection.Object
        );

    private Template SetupTemplate(Guid templateId, Guid versionId, RenderEngineType? engineType = null, IEnumerable<FieldMapping>? mappings = null)
    {
        engineType ??= RenderEngineType.Html;
        var format = engineType == RenderEngineType.Excel ? TemplateFormat.Xlsx : 
                     engineType == RenderEngineType.Docx ? TemplateFormat.Docx : TemplateFormat.Html;

        var now = TestConstants.BaselineTime;
        var template = new TemplateBuilder()
            .WithId(templateId)
            .WithProjectId(Guid.NewGuid())
            .WithName("T")
            .WithSlug("sample-template")
            .Build();
        template.SetCurrentVersion(versionId, now);
        if (mappings != null)
        {
            template.ReplaceFieldMappings(mappings, now);
        }

        _mockTemplateRepo.Setup(r => r.GetByIdWithDetailsAsync(templateId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(template);
        var version = new TemplateVersionBuilder()
            .WithId(versionId)
            .WithTemplateId(templateId)
            .WithVersion(1)
            .WithStorageKey("templates/sample-template.html")
            .WithFormat(format)
            .WithStatus("Published")
            .WithCommitMessage("Commit")
            .WithTime(now)
            .Build();
        _mockTemplateRepo.Setup(r => r.GetVersionByIdAsync(versionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(version);

        return template;
    }

    // ── No mappings path ──────────────────────────────────────────────────

    [Fact]
    public async Task ExecuteAsync_WithNoMappings_ShouldPassRawJsonToEngine()
    {
        var templateId = Guid.NewGuid();
        var versionId = Guid.NewGuid();
        SetupTemplate(templateId, versionId);

        _mockStorage.Setup(s => s.DownloadAsync(StorageBuckets.Templates, "templates/sample-template.html", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MemoryStream("<html></html>"u8.ToArray()));
        _mockHtmlEngine.Setup(e => e.RenderAsync(It.IsAny<Stream>(), It.IsAny<string>(), OutputFormat.Pdf, It.IsAny<CancellationToken>()))
            .ReturnsAsync([0x25, 0x50, 0x44, 0x46]);

        var sampleData = JsonDocument.Parse("{\"name\":\"John\"}").RootElement;

        var result = await CreateSut().ExecuteAsync(templateId, sampleData);

        result.Should().NotBeEmpty();
        _mockHtmlEngine.Verify(e => e.RenderAsync(
            It.IsAny<Stream>(),
            "{\"name\":\"John\"}",
            OutputFormat.Pdf,
            It.IsAny<CancellationToken>()), Times.Once);
        _mockApplicator.Verify(a => a.ApplyAsync(It.IsAny<JsonElement>(), It.IsAny<List<FieldMapping>>(), It.IsAny<IReadOnlyDictionary<string, ResolvedDatasetContext>>()), Times.Never);
        _mockStorage.Verify(s => s.DownloadAsync(
            StorageBuckets.Templates,
            "templates/sample-template.html",
            It.IsAny<CancellationToken>()), Times.Once);
        _mockStorage.Verify(s => s.UploadAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Stream>(),
            It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_WithUndefinedSampleData_AndNoMappings_ShouldPassEmptyJson()
    {
        var templateId = Guid.NewGuid();
        var versionId = Guid.NewGuid();
        SetupTemplate(templateId, versionId);

        _mockStorage.Setup(s => s.DownloadAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MemoryStream());
        _mockHtmlEngine.Setup(e => e.RenderAsync(It.IsAny<Stream>(), It.IsAny<string>(), OutputFormat.Pdf, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var undefined = default(JsonElement);

        await CreateSut().ExecuteAsync(templateId, undefined);

        _mockHtmlEngine.Verify(e => e.RenderAsync(
            It.IsAny<Stream>(),
            "{}",
            OutputFormat.Pdf,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── With mappings path ────────────────────────────────────────────────

    [Fact]
    public async Task ExecuteAsync_WithMappings_ShouldCallFieldMappingApplicator()
    {
        var templateId = Guid.NewGuid();
        var versionId = Guid.NewGuid();

        var mappings = new List<FieldMapping>
        {
            new FieldMappingBuilder()
                .WithTemplateId(templateId)
                .WithPlaceholder("fullName")
                .WithSourcePath("customer.name")
                .WithLabel("Full Name")
                .WithRequired(false)
                .WithSortOrder(1)
                .WithDataSourceType(DataSourceType.Json)
                .Build()
        };
        SetupTemplate(templateId, versionId, mappings: mappings);

        _mockStorage.Setup(s => s.DownloadAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MemoryStream());
        _mockApplicator.Setup(a => a.ApplyAsync(It.IsAny<JsonElement>(), It.IsAny<IEnumerable<FieldMapping>>(), It.IsAny<IReadOnlyDictionary<string, ResolvedDatasetContext>>()))
            .ReturnsAsync("{\"fullName\":\"John Doe\"}");
        _mockHtmlEngine.Setup(e => e.RenderAsync(It.IsAny<Stream>(), "{\"fullName\":\"John Doe\"}", OutputFormat.Pdf, It.IsAny<CancellationToken>()))
            .ReturnsAsync([0x25, 0x50, 0x44, 0x46]);

        var sampleData = JsonDocument.Parse("{\"customer\":{\"name\":\"John Doe\"}}").RootElement;

        var result = await CreateSut().ExecuteAsync(templateId, sampleData);

        result.Should().NotBeEmpty();
        _mockApplicator.Verify(a => a.ApplyAsync(
            It.IsAny<JsonElement>(),
            It.Is<IEnumerable<FieldMapping>>(m => m.Count() == 1),
            It.IsAny<IReadOnlyDictionary<string, ResolvedDatasetContext>>()), Times.Once);
    }

    // ── Error paths ───────────────────────────────────────────────────────

    [Fact]
    public async Task ExecuteAsync_WhenTemplateNotFound_ShouldThrowNotFoundException()
    {
        _mockTemplateRepo.Setup(r => r.GetByIdWithDetailsAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Template?)null);

        Func<Task> act = () => CreateSut().ExecuteAsync(Guid.NewGuid(), default);
        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task ExecuteAsync_WhenTemplateHasNoCurrentVersion_ShouldThrowBusinessRuleViolationException()
    {
        var templateId = Guid.NewGuid();
        var template = new TemplateBuilder()
            .WithId(templateId)
            .WithProjectId(Guid.NewGuid())
            .WithName("T")
            .WithSlug("sample-template")
            .WithoutCurrentVersion()
            .Build();
        _mockTemplateRepo.Setup(r => r.GetByIdWithDetailsAsync(templateId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(template);

        Func<Task> act = () => CreateSut().ExecuteAsync(templateId, default);
        await act.Should().ThrowAsync<BusinessRuleViolationException>();
    }

    [Fact]
    public async Task ExecuteAsync_WhenNoEngineForType_ShouldThrowRenderException()
    {
        var templateId = Guid.NewGuid();
        var versionId = Guid.NewGuid();
        SetupTemplate(templateId, versionId, RenderEngineType.Docx);

        _mockStorage.Setup(s => s.DownloadAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MemoryStream());

        // Only Html engine registered — Docx engine is missing
        Func<Task> act = () => CreateSut().ExecuteAsync(templateId, default);
        await act.Should().ThrowAsync<SmkDoc.Application.Common.Exceptions.RenderException>();
    }

    // ── Stream execution tests ──────────────────────────────────────────

    [Fact]
    public async Task ExecuteStreamAsync_WithNoMappings_ShouldStreamPdfDirectly()
    {
        var templateId = Guid.NewGuid();
        var versionId = Guid.NewGuid();
        SetupTemplate(templateId, versionId);

        var expectedStream = new MemoryStream([0x25, 0x50, 0x44, 0x46]);
        _mockStorage.Setup(s => s.DownloadAsync(StorageBuckets.Templates, "templates/sample-template.html", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MemoryStream("<html></html>"u8.ToArray()));
        _mockHtmlEngine.Setup(e => e.RenderStreamAsync(It.IsAny<Stream>(), It.IsAny<string>(), OutputFormat.Pdf, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedStream);

        var sampleData = JsonDocument.Parse("{\"name\":\"John\"}").RootElement;

        var resultStream = await CreateSut().ExecuteStreamAsync(templateId, sampleData);

        resultStream.Should().BeSameAs(expectedStream);
        _mockHtmlEngine.Verify(e => e.RenderStreamAsync(
            It.IsAny<Stream>(),
            "{\"name\":\"John\"}",
            OutputFormat.Pdf,
            It.IsAny<CancellationToken>()), Times.Once);
        _mockStorage.Verify(s => s.UploadAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Stream>(),
            It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteStreamAsync_WithMappings_ShouldCallApplicatorAndStreamPdf()
    {
        var templateId = Guid.NewGuid();
        var versionId = Guid.NewGuid();

        var mappings = new List<FieldMapping>
        {
            new FieldMappingBuilder()
                .WithTemplateId(templateId)
                .WithPlaceholder("fullName")
                .WithSourcePath("customer.name")
                .WithLabel("Full Name")
                .WithRequired(false)
                .WithSortOrder(1)
                .WithDataSourceType(DataSourceType.Json)
                .Build()
        };
        SetupTemplate(templateId, versionId, mappings: mappings);

        var expectedStream = new MemoryStream([0x25, 0x50, 0x44, 0x46]);
        _mockStorage.Setup(s => s.DownloadAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MemoryStream());
        _mockApplicator.Setup(a => a.ApplyAsync(It.IsAny<JsonElement>(), It.IsAny<IEnumerable<FieldMapping>>(), It.IsAny<IReadOnlyDictionary<string, ResolvedDatasetContext>>()))
            .ReturnsAsync("{\"fullName\":\"John Doe\"}");
        _mockHtmlEngine.Setup(e => e.RenderStreamAsync(It.IsAny<Stream>(), "{\"fullName\":\"John Doe\"}", OutputFormat.Pdf, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedStream);

        var sampleData = JsonDocument.Parse("{\"customer\":{\"name\":\"John Doe\"}}").RootElement;

        var resultStream = await CreateSut().ExecuteStreamAsync(templateId, sampleData);

        resultStream.Should().BeSameAs(expectedStream);
        _mockApplicator.Verify(a => a.ApplyAsync(
            It.IsAny<JsonElement>(),
            It.Is<IEnumerable<FieldMapping>>(m => m.Count() == 1),
            It.IsAny<IReadOnlyDictionary<string, ResolvedDatasetContext>>()), Times.Once);
    }
}
