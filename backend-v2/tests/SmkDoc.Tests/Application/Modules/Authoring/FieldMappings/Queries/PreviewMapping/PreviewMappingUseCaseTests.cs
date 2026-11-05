using System.Text.Json;
using SmkDoc.Application.Common;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.Authoring.FieldMappings.Queries.PreviewMapping;
using SmkDoc.Application.Modules.Authoring.FieldMappings.Services;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Exceptions;
using SmkDoc.Domain.ValueObjects.Ids;
using SmkDoc.Tests.Common.Builders;

namespace SmkDoc.Tests.Application.Modules.Authoring.FieldMappings.Queries.PreviewMapping;

public sealed class PreviewMappingUseCaseTests
{
    private readonly ITemplateRepository _templateRepo = Substitute.For<ITemplateRepository>();
    private readonly IStorageService _storage = Substitute.For<IStorageService>();
    private readonly IRenderEngine _htmlEngine = Substitute.For<IRenderEngine>();
    private readonly IRenderEngineFactory _engineFactory = Substitute.For<IRenderEngineFactory>();
    private readonly IDataBindingOrchestrator _orchestrator = Substitute.For<IDataBindingOrchestrator>();

    public PreviewMappingUseCaseTests()
    {
        _htmlEngine.EngineType.Returns(RenderEngineType.Html);
        _engineFactory.GetEngine(RenderEngineType.Html).Returns(_htmlEngine);
        _engineFactory.GetEngine(Arg.Is<RenderEngineType>(t => t != RenderEngineType.Html))
            .Throws(new NotSupportedException("No render engine registered for type"));
    }

    private PreviewMappingUseCase CreateSut() =>
        new(
            _templateRepo,
            _storage,
            _engineFactory,
            _orchestrator
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

        template.PublishVersion(version, now);

        if (mappings != null)
        {
            template.ReplaceFieldMappings(mappings, now);
        }

        _templateRepo.GetByIdWithDetailsAsync(new TemplateId(templateId), Arg.Any<CancellationToken>())
            .Returns(template);
        _templateRepo.GetVersionByIdAsync(versionId, Arg.Any<CancellationToken>())
            .Returns(version);

        return template;
    }

    [Fact]
    public async Task ExecuteAsync_WithNoMappings_ShouldPassRawJsonToEngine()
    {
        var templateId = Guid.NewGuid();
        var versionId = Guid.NewGuid();
        SetupTemplate(templateId, versionId);

        var expectedStream = new MemoryStream([0x25, 0x50, 0x44, 0x46]);
        _storage.DownloadAsync(StorageBuckets.Templates, "templates/sample-template.html", Arg.Any<CancellationToken>())
            .Returns(new MemoryStream("<html></html>"u8.ToArray()));
        _htmlEngine.RenderStreamAsync(Arg.Any<Stream>(), Arg.Any<string>(), OutputFormat.Pdf, Arg.Any<CancellationToken>())
            .Returns(expectedStream);
        _orchestrator.AssemblePayloadAsync(Arg.Any<Template>(), Arg.Any<JsonElement>(), Arg.Any<CancellationToken>())
            .Returns("{\"name\":\"John\"}");

        var sampleData = JsonDocument.Parse("{\"name\":\"John\"}").RootElement;
        var query = new PreviewMappingQuery(templateId, sampleData);

        var resultStream = await CreateSut().ExecuteAsync(query);

        resultStream.Should().BeSameAs(expectedStream);
        await _htmlEngine.Received(1).RenderStreamAsync(
            Arg.Any<Stream>(),
            "{\"name\":\"John\"}",
            OutputFormat.Pdf,
            Arg.Any<CancellationToken>());
        await _orchestrator.Received(1).AssemblePayloadAsync(
            Arg.Any<Template>(),
            Arg.Any<JsonElement>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WithUndefinedSampleData_ShouldAssemblePayloadAndPassToEngine()
    {
        var templateId = Guid.NewGuid();
        var versionId = Guid.NewGuid();
        SetupTemplate(templateId, versionId);

        var expectedStream = new MemoryStream();
        _storage.DownloadAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new MemoryStream());
        _htmlEngine.RenderStreamAsync(Arg.Any<Stream>(), Arg.Any<string>(), OutputFormat.Pdf, Arg.Any<CancellationToken>())
            .Returns(expectedStream);
        _orchestrator.AssemblePayloadAsync(Arg.Any<Template>(), Arg.Any<JsonElement>(), Arg.Any<CancellationToken>())
            .Returns("{}");

        var undefined = default(JsonElement);
        var query = new PreviewMappingQuery(templateId, undefined);

        var resultStream = await CreateSut().ExecuteAsync(query);

        resultStream.Should().BeSameAs(expectedStream);
        await _htmlEngine.Received(1).RenderStreamAsync(
            Arg.Any<Stream>(),
            "{}",
            OutputFormat.Pdf,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WithMappings_ShouldCallDataBindingOrchestrator()
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
        _storage.DownloadAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new MemoryStream());
        _orchestrator.AssemblePayloadAsync(Arg.Any<Template>(), Arg.Any<JsonElement>(), Arg.Any<CancellationToken>())
            .Returns("{\"fullName\":\"John Doe\"}");
        _htmlEngine.RenderStreamAsync(Arg.Any<Stream>(), "{\"fullName\":\"John Doe\"}", OutputFormat.Pdf, Arg.Any<CancellationToken>())
            .Returns(expectedStream);

        var sampleData = JsonDocument.Parse("{\"customer\":{\"name\":\"John Doe\"}}").RootElement;
        var query = new PreviewMappingQuery(templateId, sampleData);

        var resultStream = await CreateSut().ExecuteAsync(query);

        resultStream.Should().BeSameAs(expectedStream);
        await _orchestrator.Received(1).AssemblePayloadAsync(
            Arg.Any<Template>(),
            Arg.Any<JsonElement>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenTemplateNotFound_ShouldThrowNotFoundException()
    {
        _templateRepo.GetByIdWithDetailsAsync(Arg.Any<TemplateId>(), Arg.Any<CancellationToken>())
            .Returns((Template?)null);

        var act = () => CreateSut().ExecuteAsync(new PreviewMappingQuery(Guid.NewGuid(), default));
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
        _templateRepo.GetByIdWithDetailsAsync(new TemplateId(templateId), Arg.Any<CancellationToken>())
            .Returns(template);

        var act = () => CreateSut().ExecuteAsync(new PreviewMappingQuery(templateId, default));
        await act.Should().ThrowAsync<BusinessRuleViolationException>();
    }

    [Fact]
    public async Task ExecuteAsync_WhenNoEngineForType_ShouldThrowRenderException()
    {
        var templateId = Guid.NewGuid();
        var versionId = Guid.NewGuid();
        SetupTemplate(templateId, versionId, RenderEngineType.Docx);

        _storage.DownloadAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new MemoryStream());

        var act = () => CreateSut().ExecuteAsync(new PreviewMappingQuery(templateId, default));
        await act.Should().ThrowAsync<SmkDoc.Application.Common.Exceptions.RenderException>();
    }
}
