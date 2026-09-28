using System.Linq.Expressions;
using System.Text.Json;
using FluentAssertions;
using Moq;
using SmkDoc.Application.Common;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.Authoring.FieldMappings.DTOs;
using SmkDoc.Application.Modules.Authoring.FieldMappings;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Enums;
using SmkDoc.Domain.ValueObjects;
using SmkDoc.Domain.Exceptions;
using Xunit;

namespace SmkDoc.Tests.Application.UseCases.FieldMappings;

public class PreviewMappingUseCaseTests
{
    private readonly Mock<IRepository<Template>>        _mockTemplateRepo    = new();
    private readonly Mock<IRepository<TemplateVersion>> _mockVersionRepo     = new();
    private readonly Mock<IRepository<FieldMapping>>    _mockMappingRepo     = new();
    private readonly Mock<IRepository<TemplateDataset>> _mockTdRepo          = new();
    private readonly Mock<IRepository<Dataset>>         _mockDatasetRepo     = new();
    private readonly Mock<IRepository<DataConnection>>  _mockConnectionRepo  = new();
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
            _mockMappingRepo.Object,
            _mockTdRepo.Object,
            _mockDatasetRepo.Object,
            _mockConnectionRepo.Object,
            _mockStorage.Object,
            [_mockHtmlEngine.Object],
            _mockApplicator.Object,
            _mockDataProtection.Object
        );
    }

    private void SetupTemplate(Guid templateId, Guid versionId, RenderEngineType? engineType = null)
    {
        engineType ??= RenderEngineType.Html;
        TemplateFormat? format = engineType == RenderEngineType.Excel ? TemplateFormat.Xlsx : 
                                 engineType == RenderEngineType.Docx ? TemplateFormat.Docx : TemplateFormat.Html;


        var template = new Template(Guid.NewGuid(), "T", "t", null) { Id = templateId };
        template.SetCurrentVersion(versionId);
        _mockTemplateRepo.Setup(r => r.GetByIdAsync(templateId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(template);
        _mockVersionRepo.Setup(r => r.GetByIdAsync(versionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TemplateVersion(templateId, 1, $"templates/t.html", format, "Published", "Commit") { Id = versionId });
    }

    // ── No mappings path ──────────────────────────────────────────────────

    [Fact]
    public async Task ExecuteAsync_WithNoMappings_ShouldPassRawJsonToEngine()
    {
        var templateId = Guid.NewGuid();
        var versionId  = Guid.NewGuid();
        SetupTemplate(templateId, versionId);

        _mockMappingRepo.Setup(r => r.ListAsync(It.IsAny<Expression<Func<FieldMapping, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        _mockStorage.Setup(s => s.DownloadAsync(StorageBuckets.Templates, "templates/t.html", It.IsAny<CancellationToken>()))
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

        _mockMappingRepo.Setup(r => r.ListAsync(It.IsAny<Expression<Func<FieldMapping, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        _mockStorage.Setup(s => s.DownloadAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MemoryStream());
        _mockHtmlEngine.Setup(e => e.RenderAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<OutputFormat>(), It.IsAny<CancellationToken>()))
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
        SetupTemplate(templateId, versionId);

        var mappings = new List<FieldMapping>
        {
            new FieldMapping(templateId, "fullName", "customer.name", "Full Name", false, 1, DataSourceType.Json)
        };
        _mockMappingRepo.Setup(r => r.ListAsync(It.IsAny<Expression<Func<FieldMapping, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(mappings);
        _mockTdRepo.Setup(r => r.ListAsync(It.IsAny<Expression<Func<TemplateDataset, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        _mockStorage.Setup(s => s.DownloadAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MemoryStream());
        _mockApplicator.Setup(a => a.ApplyAsync(It.IsAny<JsonElement>(), mappings, It.IsAny<IReadOnlyDictionary<string, ResolvedDatasetContext>>()))
            .ReturnsAsync("{\"fullName\":\"John Doe\"}");
        _mockHtmlEngine.Setup(e => e.RenderAsync(It.IsAny<Stream>(), "{\"fullName\":\"John Doe\"}", OutputFormat.Pdf, It.IsAny<CancellationToken>()))
            .ReturnsAsync([0x25, 0x50, 0x44, 0x46]);

        var sampleData = JsonDocument.Parse("{\"customer\":{\"name\":\"John Doe\"}}").RootElement;

        var result = await CreateUseCase().ExecuteAsync(templateId, sampleData);

        result.Should().NotBeEmpty();
        _mockApplicator.Verify(a => a.ApplyAsync(
            It.IsAny<JsonElement>(),
            mappings,
            It.IsAny<IReadOnlyDictionary<string, ResolvedDatasetContext>>()), Times.Once);
    }

    // ── Error paths ───────────────────────────────────────────────────────

    [Fact]
    public async Task ExecuteAsync_WhenTemplateNotFound_ShouldThrowNotFoundException()
    {
        _mockTemplateRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Template?)null);

        Func<Task> act = () => CreateUseCase().ExecuteAsync(Guid.NewGuid(), default);
        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task ExecuteAsync_WhenTemplateHasNoCurrentVersion_ShouldThrowInvalidOperationException()
    {
        var templateId = Guid.NewGuid();
        _mockTemplateRepo.Setup(r => r.GetByIdAsync(templateId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Template(Guid.NewGuid(), "T", "t", null) { Id = templateId });
        _mockMappingRepo.Setup(r => r.ListAsync(It.IsAny<Expression<Func<FieldMapping, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        Func<Task> act = () => CreateUseCase().ExecuteAsync(templateId, default);
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task ExecuteAsync_WhenNoEngineForType_ShouldThrowInvalidOperationException()
    {
        var templateId = Guid.NewGuid();
        var versionId  = Guid.NewGuid();

        var template = new Template(Guid.NewGuid(), "T", "t", null) { Id = templateId };
        template.SetCurrentVersion(versionId);
        _mockTemplateRepo.Setup(r => r.GetByIdAsync(templateId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(template);
        _mockVersionRepo.Setup(r => r.GetByIdAsync(versionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TemplateVersion(templateId, 1, "templates/t.docx", TemplateFormat.Docx, "Published", "Commit") { Id = versionId });
        _mockMappingRepo.Setup(r => r.ListAsync(It.IsAny<Expression<Func<FieldMapping, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
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

        _mockMappingRepo.Setup(r => r.ListAsync(It.IsAny<Expression<Func<FieldMapping, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        _mockStorage.Setup(s => s.DownloadAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MemoryStream());
        _mockHtmlEngine.Setup(e => e.RenderAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<OutputFormat>(), It.IsAny<CancellationToken>()))
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

        _mockMappingRepo.Setup(r => r.ListAsync(It.IsAny<Expression<Func<FieldMapping, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        _mockStorage.Setup(s => s.DownloadAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MemoryStream());
        _mockHtmlEngine.Setup(e => e.RenderAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<OutputFormat>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        await CreateUseCase().ExecuteAsync(templateId, default);

        _mockStorage.Verify(s => s.DownloadAsync(
            StorageBuckets.Templates,
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
