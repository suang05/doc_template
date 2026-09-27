using System.Linq.Expressions;
using System.Text.Json;
using FluentAssertions;
using Moq;
using SmkDoc.Application.Common;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Common.Models;
using SmkDoc.Application.Engines;
using SmkDoc.Application.UseCases.FieldMappings;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Enums;
using Xunit;

namespace SmkDoc.Tests;

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

    private void SetupTemplate(Guid templateId, Guid versionId, RenderEngineType engineType = RenderEngineType.Html)
    {
        TemplateFormat? format = engineType switch
        {
            RenderEngineType.Excel => TemplateFormat.Xlsx,
            RenderEngineType.Docx  => TemplateFormat.Docx,
            _                      => TemplateFormat.Html,
        };

        _mockTemplateRepo.Setup(r => r.GetByIdAsync(templateId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Template { Id = templateId, Name = "T", Slug = "t", CurrentVersionId = versionId });
        _mockVersionRepo.Setup(r => r.GetByIdAsync(versionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TemplateVersion
            {
                Id = versionId, TemplateId = templateId,
                StorageKey = $"templates/t.html", FileFormat = format
            });
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
        _mockApplicator.Verify(a => a.ApplyAsync(It.IsAny<JsonElement>(), It.IsAny<List<FieldMapping>>(), It.IsAny<IReadOnlyDictionary<string, ResolvedDataset>>()), Times.Never);
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
            new() { TemplateId = templateId, Placeholder = "fullName", SourcePath = "customer.name" }
        };
        _mockMappingRepo.Setup(r => r.ListAsync(It.IsAny<Expression<Func<FieldMapping, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(mappings);
        _mockTdRepo.Setup(r => r.ListAsync(It.IsAny<Expression<Func<TemplateDataset, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        _mockStorage.Setup(s => s.DownloadAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MemoryStream());
        _mockApplicator.Setup(a => a.ApplyAsync(It.IsAny<JsonElement>(), mappings, It.IsAny<IReadOnlyDictionary<string, ResolvedDataset>>()))
            .ReturnsAsync("{\"fullName\":\"John Doe\"}");
        _mockHtmlEngine.Setup(e => e.RenderAsync(It.IsAny<Stream>(), "{\"fullName\":\"John Doe\"}", OutputFormat.Pdf, It.IsAny<CancellationToken>()))
            .ReturnsAsync([0x25, 0x50, 0x44, 0x46]);

        var sampleData = JsonDocument.Parse("{\"customer\":{\"name\":\"John Doe\"}}").RootElement;

        var result = await CreateUseCase().ExecuteAsync(templateId, sampleData);

        result.Should().NotBeEmpty();
        _mockApplicator.Verify(a => a.ApplyAsync(
            It.IsAny<JsonElement>(),
            mappings,
            It.IsAny<IReadOnlyDictionary<string, ResolvedDataset>>()), Times.Once);
    }

    // ── Error paths ───────────────────────────────────────────────────────

    [Fact]
    public async Task ExecuteAsync_WhenTemplateNotFound_ShouldThrowKeyNotFoundException()
    {
        _mockTemplateRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Template?)null);

        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => CreateUseCase().ExecuteAsync(Guid.NewGuid(), default));
    }

    [Fact]
    public async Task ExecuteAsync_WhenTemplateHasNoCurrentVersion_ShouldThrowInvalidOperationException()
    {
        var templateId = Guid.NewGuid();
        _mockTemplateRepo.Setup(r => r.GetByIdAsync(templateId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Template { Id = templateId, Slug = "t", CurrentVersionId = null });
        _mockMappingRepo.Setup(r => r.ListAsync(It.IsAny<Expression<Func<FieldMapping, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => CreateUseCase().ExecuteAsync(templateId, default));
    }

    [Fact]
    public async Task ExecuteAsync_WhenNoEngineForType_ShouldThrowInvalidOperationException()
    {
        var templateId = Guid.NewGuid();
        var versionId  = Guid.NewGuid();

        _mockTemplateRepo.Setup(r => r.GetByIdAsync(templateId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Template { Id = templateId, Slug = "t", CurrentVersionId = versionId });
        _mockVersionRepo.Setup(r => r.GetByIdAsync(versionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TemplateVersion
            {
                Id = versionId, TemplateId = templateId,
                StorageKey = "templates/t.docx", FileFormat = TemplateFormat.Docx
            });
        _mockMappingRepo.Setup(r => r.ListAsync(It.IsAny<Expression<Func<FieldMapping, bool>>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        _mockStorage.Setup(s => s.DownloadAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MemoryStream());

        // Only Html engine registered — Docx engine is missing
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => CreateUseCase().ExecuteAsync(templateId, default));
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
