using System.Text.Json;
using FluentAssertions;
using Moq;
using SmkDoc.Application.Common;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.Authoring.FieldMappings.Queries.PreviewMapping;
using SmkDoc.Application.Modules.Authoring.FieldMappings.DTOs;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Enums;
using SmkDoc.Domain.Exceptions;
using SmkDoc.Domain.Interfaces;
using SmkDoc.Domain.ValueObjects;
using SmkDoc.Tests.Common.Factories;
using Xunit;

namespace SmkDoc.Tests.Application.Modules.Authoring.FieldMappings;

public class PreviewMappingUseCaseTests
{
    private readonly Mock<ITemplateRepository>          _mockTemplateRepo    = new();
    private readonly Mock<IRepository<TemplateVersion>> _mockVersionRepo     = new();
    private readonly Mock<IDatasetRepository>           _mockDatasetRepo     = new();
    private readonly Mock<IDataConnectionRepository>    _mockConnectionRepo  = new();
    private readonly Mock<IStorageService>              _mockStorage         = new();
    private readonly Mock<IRenderEngine>                _mockHtmlEngine      = new();
    private readonly Mock<IFieldMappingApplicatorService> _mockApplicator    = new();
    private readonly Mock<IDataProtectionService>       _mockDataProtection  = new();

    private PreviewMappingUseCase CreateUseCase()
    {
        _mockHtmlEngine.Setup(e => e.EngineType).Returns(RenderEngineType.Html);
        return new PreviewMappingUseCase(
            _mockTemplateRepo.Object,
            _mockVersionRepo.Object,
            _mockDatasetRepo.Object,
            _mockConnectionRepo.Object,
            _mockStorage.Object,
            [_mockHtmlEngine.Object],
            _mockApplicator.Object,
            _mockDataProtection.Object
        );
    }

    private Template SetupTemplate(Guid templateId, Guid versionId, RenderEngineType? engineType = null, IEnumerable<FieldMapping>? mappings = null)
    {
        engineType ??= RenderEngineType.Html;
        TemplateFormat? format = engineType == RenderEngineType.Excel ? TemplateFormat.Xlsx : 
                                 engineType == RenderEngineType.Docx ? TemplateFormat.Docx : TemplateFormat.Html;

        var now = DateTimeOffset.UtcNow;
        var template = TemplateTestFactory.Create(templateId, Guid.NewGuid(), "T", "sample-template");
        template.SetCurrentVersion(versionId, now);
        if (mappings != null)
        {
            template.ReplaceFieldMappings(mappings, now);
        }

        _mockTemplateRepo.Setup(r => r.GetByIdWithDetailsAsync(templateId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(template);
        _mockTemplateRepo.Setup(r => r.GetByIdAsync(templateId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(template);
        _mockVersionRepo.Setup(r => r.GetByIdAsync(versionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(TemplateVersionTestFactory.Create(versionId, templateId, 1, $"templates/sample-template.html", format, "Published", "Commit", now));

        return template;
    }

    // ── No mappings path ──────────────────────────────────────────────────

    [Fact]
    public async Task ExecuteAsync_WithNoMappings_ShouldPassRawJsonToEngine()
    {
        var templateId = Guid.NewGuid();
        var versionId  = Guid.NewGuid();
        SetupTemplate(templateId, versionId);

        _mockStorage.Setup(s => s.DownloadAsync(StorageBuckets.Templates, "templates/sample-template.html", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MemoryStream("<html></html>"u8.ToArray()));
        _mockHtmlEngine.Setup(e => e.RenderAsync(It.IsAny<Stream>(), It.IsAny<string>(), OutputFormat.Pdf, It.IsAny<CancellationToken>()))
            .ReturnsAsync([0x25, 0x50, 0x44, 0x46]);

        var sampleData = JsonDocument.Parse("{\"name\":\"John\"}").RootElement;

        var result = await CreateUseCase().ExecuteAsync(templateId, sampleData);

        result.Should().NotBeEmpty();
        _mockHtmlEngine.Verify(e => e.RenderAsync(
            It.IsAny<Stream>(),
            "{\"name\":\"John\"}",
            OutputFormat.Pdf,
            It.IsAny<CancellationToken>()), Times.Once);
        _mockApplicator.Verify(a => a.ApplyAsync(It.IsAny<JsonElement>(), It.IsAny<List<FieldMapping>>(), It.IsAny<IReadOnlyDictionary<string, ResolvedDatasetContext>>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_WithUndefinedSampleData_AndNoMappings_ShouldPassEmptyJson()
    {
        var templateId = Guid.NewGuid();
        var versionId  = Guid.NewGuid();
        SetupTemplate(templateId, versionId);

        _mockStorage.Setup(s => s.DownloadAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MemoryStream());
        _mockHtmlEngine.Setup(e => e.RenderAsync(It.IsAny<Stream>(), It.IsAny<string>(), OutputFormat.Pdf, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var undefined = default(JsonElement); // JsonValueKind.Undefined

        await CreateUseCase().ExecuteAsync(templateId, undefined);

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
        var versionId  = Guid.NewGuid();

        var mappings = new List<FieldMapping>
        {
            FieldMapping.Create(templateId, "fullName", "customer.name", "Full Name", false, 1, DateTimeOffset.UtcNow, DataSourceType.Json)
        };
        SetupTemplate(templateId, versionId, mappings: mappings);

        _mockStorage.Setup(s => s.DownloadAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MemoryStream());
        _mockApplicator.Setup(a => a.ApplyAsync(It.IsAny<JsonElement>(), It.IsAny<IEnumerable<FieldMapping>>(), It.IsAny<IReadOnlyDictionary<string, ResolvedDatasetContext>>()))
            .ReturnsAsync("{\"fullName\":\"John Doe\"}");
        _mockHtmlEngine.Setup(e => e.RenderAsync(It.IsAny<Stream>(), "{\"fullName\":\"John Doe\"}", OutputFormat.Pdf, It.IsAny<CancellationToken>()))
            .ReturnsAsync([0x25, 0x50, 0x44, 0x46]);

        var sampleData = JsonDocument.Parse("{\"customer\":{\"name\":\"John Doe\"}}").RootElement;

        var result = await CreateUseCase().ExecuteAsync(templateId, sampleData);

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
        _mockTemplateRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Template?)null);

        Func<Task> act = () => CreateUseCase().ExecuteAsync(Guid.NewGuid(), default);
        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task ExecuteAsync_WhenTemplateHasNoCurrentVersion_ShouldThrowInvalidOperationException()
    {
        var templateId = Guid.NewGuid();
        var template = TemplateTestFactory.Create(templateId, Guid.NewGuid(), "T", "sample-template");
        _mockTemplateRepo.Setup(r => r.GetByIdWithDetailsAsync(templateId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(template);
        _mockTemplateRepo.Setup(r => r.GetByIdAsync(templateId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(template);

        Func<Task> act = () => CreateUseCase().ExecuteAsync(templateId, default);
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task ExecuteAsync_WhenNoEngineForType_ShouldThrowInvalidOperationException()
    {
        var templateId = Guid.NewGuid();
        var versionId  = Guid.NewGuid();

        var now = DateTimeOffset.UtcNow;
        var template = TemplateTestFactory.Create(templateId, Guid.NewGuid(), "T", "sample-template");
        template.SetCurrentVersion(versionId, now);
        _mockTemplateRepo.Setup(r => r.GetByIdWithDetailsAsync(templateId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(template);
        _mockTemplateRepo.Setup(r => r.GetByIdAsync(templateId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(template);
        _mockVersionRepo.Setup(r => r.GetByIdAsync(versionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(TemplateVersionTestFactory.Create(versionId, templateId, 1, "templates/t.docx", TemplateFormat.Docx, "Published", "Commit", now));
        _mockStorage.Setup(s => s.DownloadAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MemoryStream());

        // Only Html engine registered — Docx engine is missing
        Func<Task> act = () => CreateUseCase().ExecuteAsync(templateId, default);
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    // ── Side-effect guarantee ─────────────────────────────────────────────

    [Fact]
    public async Task ExecuteAsync_ShouldNeverCallStorageUpload()
    {
        var templateId = Guid.NewGuid();
        var versionId  = Guid.NewGuid();
        SetupTemplate(templateId, versionId);

        _mockStorage.Setup(s => s.DownloadAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MemoryStream());
        _mockHtmlEngine.Setup(e => e.RenderAsync(It.IsAny<Stream>(), It.IsAny<string>(), OutputFormat.Pdf, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        await CreateUseCase().ExecuteAsync(templateId, default);

        _mockStorage.Verify(s => s.UploadAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Stream>(),
            It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldDownloadTemplateFromTemplatesBucket()
    {
        var templateId = Guid.NewGuid();
        var versionId  = Guid.NewGuid();
        SetupTemplate(templateId, versionId);

        _mockStorage.Setup(s => s.DownloadAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MemoryStream());
        _mockHtmlEngine.Setup(e => e.RenderAsync(It.IsAny<Stream>(), It.IsAny<string>(), OutputFormat.Pdf, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        await CreateUseCase().ExecuteAsync(templateId, default);

        _mockStorage.Verify(s => s.DownloadAsync(
            StorageBuckets.Templates,
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
