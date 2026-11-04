using System.Text;
using System.Text.Json;
using FluentAssertions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using SmkDoc.Application.Common;
using SmkDoc.Application.Common.Exceptions;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.Rendering.Documents.Services;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Enums;
using SmkDoc.Domain.Interfaces;
using SmkDoc.Domain.ValueObjects.Ids;
using SmkDoc.Domain.ValueObjects.Validation;
using SmkDoc.Tests.Common;
using SmkDoc.Tests.Common.Builders;
using Xunit;

namespace SmkDoc.Tests.Application.Modules.Rendering.Documents.Services;

public sealed class DocumentRenderPipelineTests
{
    private readonly IRenderEngineFactory _engineFactory = Substitute.For<IRenderEngineFactory>();
    private readonly IRenderEngine _engine = Substitute.For<IRenderEngine>();
    private readonly IDocumentDataPreparationService _dataPreparationService = Substitute.For<IDocumentDataPreparationService>();
    private readonly IStorageService _storageService = Substitute.For<IStorageService>();
    private readonly IDocumentAuditService _auditService = Substitute.For<IDocumentAuditService>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();

    public DocumentRenderPipelineTests()
    {
        _engine.EngineType.Returns(RenderEngineType.Html);
        _engineFactory.GetEngine(Arg.Any<RenderEngineType>()).Returns(_engine);
        _unitOfWork.CommitAsync(Arg.Any<CancellationToken>()).Returns(1);
    }

    private DocumentRenderPipeline CreateSut() => new(
        _engineFactory,
        _dataPreparationService,
        _storageService,
        _auditService,
        _unitOfWork);

    private static (Template Template, TemplateVersion Version) CreateSampleTemplate()
    {
        var templateId = Guid.NewGuid();
        var versionId = Guid.NewGuid();
        var template = TemplateBuilder.ATemplate()
            .WithId(templateId)
            .WithSlug("test-doc")
            .WithName("Test Doc")
            .Build();

        var version = TemplateVersionBuilder.AVersion()
            .WithId(versionId)
            .WithTemplateId(templateId)
            .WithVersion(1)
            .WithStorageKey("templates/test-doc.html")
            .WithFormat(TemplateFormat.Html)
            .Build();

        template.PublishVersion(version, TestConstants.BaselineTime);
        return (template, version);
    }

    [Fact]
    public async Task RenderAndUploadAsync_WhenValid_PreparesData_Renders_Spools_AndUploads()
    {
        // Arrange
        var (template, version) = CreateSampleTemplate();
        using var jsonDoc = JsonDocument.Parse("{\"name\":\"Alice\"}");
        var preparedJson = "{\"name\":\"Alice\",\"mapped\":true}";

        _dataPreparationService.PrepareDataAsync(version, Arg.Any<JsonElement>(), false, Arg.Any<CancellationToken>())
            .Returns(new PreparedDocumentData(preparedJson, SchemaValidationResult.Success()));

        var templateBytes = Encoding.UTF8.GetBytes("<html><body>{{name}}</body></html>");
        _storageService.DownloadAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new MemoryStream(templateBytes));

        var outputBytes = Encoding.UTF8.GetBytes("%PDF-1.4 Mock PDF Output");
        _engine.RenderStreamAsync(Arg.Any<Stream>(), preparedJson, OutputFormat.Pdf, Arg.Any<CancellationToken>())
            .Returns(new MemoryStream(outputBytes));

        const string outputKey = "outputs/tenant/test.pdf";

        // Act
        var result = await CreateSut().RenderAndUploadAsync(
            template, version, jsonDoc.RootElement, false, OutputFormat.Pdf, outputKey);

        // Assert
        result.Should().NotBeNull();
        result.PreparedDataJson.Should().Be(preparedJson);
        result.FileSizeBytes.Should().Be(outputBytes.Length);
        result.Sha256Hex.Should().NotBeNullOrEmpty();

        await _storageService.Received(1).UploadAsync(
            StorageBuckets.Outputs,
            outputKey,
            Arg.Any<Stream>(),
            OutputFormat.Pdf.MimeType,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RenderAndUploadAsync_WhenSchemaValidationFails_LogsAuditAndThrowsSchemaValidationException()
    {
        // Arrange
        var (template, version) = CreateSampleTemplate();
        using var jsonDoc = JsonDocument.Parse("{}");
        var errors = new List<ValidationErrorItem>
        {
            new("/", "required", "Missing required field")
        };

        _dataPreparationService.PrepareDataAsync(version, Arg.Any<JsonElement>(), false, Arg.Any<CancellationToken>())
            .Returns(new PreparedDocumentData("{}", SchemaValidationResult.Failure(errors)));

        // Act
        var act = () => CreateSut().RenderAndUploadAsync(
            template, version, jsonDoc.RootElement, false, OutputFormat.Pdf, "outputs/test.pdf");

        // Assert
        await act.Should().ThrowAsync<SchemaValidationException>();
        await _auditService.Received(1).LogValidationFailureAsync(
            template.Id.Value,
            version.Id,
            "{}",
            OutputFormat.Pdf,
            Arg.Any<int>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());

        await _unitOfWork.Received(1).CommitAsync(Arg.Any<CancellationToken>());

        await _engine.DidNotReceive().RenderStreamAsync(
            Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<OutputFormat>(), Arg.Any<CancellationToken>());
        await _storageService.DidNotReceive().UploadAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RenderAndUploadAsync_WhenEngineThrows_LogsAuditFailureAndRethrows()
    {
        // Arrange
        var (template, version) = CreateSampleTemplate();
        using var jsonDoc = JsonDocument.Parse("{\"key\":\"value\"}");

        _dataPreparationService.PrepareDataAsync(version, Arg.Any<JsonElement>(), false, Arg.Any<CancellationToken>())
            .Returns(new PreparedDocumentData("{\"key\":\"value\"}", SchemaValidationResult.Success()));

        _storageService.DownloadAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new MemoryStream(Encoding.UTF8.GetBytes("<html></html>")));

        _engine.RenderStreamAsync(Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<OutputFormat>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Gotenberg process killed"));

        // Act
        var act = () => CreateSut().RenderAndUploadAsync(
            template, version, jsonDoc.RootElement, false, OutputFormat.Pdf, "outputs/test.pdf");

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("Gotenberg process killed");
        await _auditService.Received(1).LogFailureAsync(
            template.Id.Value,
            version.Id,
            "{\"key\":\"value\"}",
            OutputFormat.Pdf,
            Arg.Any<int>(),
            "Gotenberg process killed",
            Arg.Any<CancellationToken>());

        await _unitOfWork.Received(1).CommitAsync(CancellationToken.None);
    }
}

