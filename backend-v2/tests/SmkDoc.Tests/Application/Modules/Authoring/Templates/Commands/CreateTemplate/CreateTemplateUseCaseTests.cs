using System.Text;
using SmkDoc.Application.Common;
using SmkDoc.Application.Common.Behaviors;
using SmkDoc.Application.Modules.Authoring.Templates.Commands.CreateTemplate;
using SmkDoc.Application.Modules.Authoring.Templates.DTOs;
using SmkDoc.Application.Modules.Rendering.Documents.DTOs;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.ValueObjects.Ids;
using ValidationException = SmkDoc.Application.Common.Exceptions.ValidationException;

namespace SmkDoc.Tests.Application.Modules.Authoring.Templates.Commands.CreateTemplate;

public sealed class CreateTemplateUseCaseTests
{
    private readonly ITemplateRepository _templateRepo = Substitute.For<ITemplateRepository>();
    private readonly IStorageService _storageService = Substitute.For<IStorageService>();
    private readonly IDocxSecurityScanner _securityScanner = Substitute.For<IDocxSecurityScanner>();
    private readonly ICurrentProjectContext _projectContext = Substitute.For<ICurrentProjectContext>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly ISchemaInferenceService _schemaInference = Substitute.For<ISchemaInferenceService>();
    private readonly FakeTimeProvider _timeProvider = TestConstants.CreateFakeClock();

    public CreateTemplateUseCaseTests()
    {
        _projectContext.IsResolved.Returns(true);
        _projectContext.GetRequiredProjectId().Returns(new ProjectId(TestConstants.DefaultProjectId));
        _projectContext.CallerIdentity.Returns("erp-system");
        _securityScanner.Scan(Arg.Any<Stream>()).Returns(new DocxScanResult(true, []));
        _unitOfWork.CommitAsync(Arg.Any<CancellationToken>()).Returns(1);

        _schemaInference.InferFromHtml(Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<string?>())
            .Returns(("{\"$schema\":\"mock-schema\"}", "{}"));
        _schemaInference.InferSchemaFromPlaceholders(Arg.Any<IEnumerable<string>>(), Arg.Any<string?>(), Arg.Any<string?>())
            .Returns("{\"$schema\":\"mock-schema\"}");
        _schemaInference.GenerateDefaultSamplePayload(Arg.Any<IEnumerable<string>>())
            .Returns("{}");
    }

    private CreateTemplateUseCase CreateSut() => new(
        _templateRepo,
        _storageService,
        _securityScanner,
        _projectContext,
        _unitOfWork,
        _schemaInference,
        _timeProvider);

    [Fact]
    public async Task ExecuteAsync_WhenCommandIsInvalid_ShouldFailFastWithoutTouchingDb()
    {
        // Arrange
        var invalidCommand = new CreateTemplateCommand("", "INVALID_SLUG", null);
        var pipeline = new ValidatingUseCaseDecorator<CreateTemplateCommand, TemplateResultDto>(
            CreateSut(),
            [new CreateTemplateCommandValidator()]);

        // Act
        var act = () => pipeline.ExecuteAsync(invalidCommand, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ValidationException>();
        await _templateRepo.DidNotReceive().AddAsync(Arg.Any<Template>(), Arg.Any<CancellationToken>());
        await _unitOfWork.DidNotReceive().CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenProjectContextUnresolved_ShouldThrowTenantContextNotResolvedException()
    {
        // Arrange
        _projectContext.IsResolved.Returns(false);
        _projectContext.GetRequiredProjectId().Returns(_ => throw new TenantContextNotResolvedException());
        var command = new CreateTemplateCommand("Tax Invoice", "tax-invoice", "Finance");

        // Act
        var act = () => CreateSut().ExecuteAsync(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<TenantContextNotResolvedException>();
        await _templateRepo.DidNotReceive().AddAsync(Arg.Any<Template>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenSlugAlreadyExists_ShouldThrowConflictException()
    {
        // Arrange
        _templateRepo.SlugExistsAsync("tax-invoice", Arg.Any<CancellationToken>())
            .Returns(true);

        var command = new CreateTemplateCommand("Tax Invoice", "tax-invoice", "Finance");

        // Act
        var act = () => CreateSut().ExecuteAsync(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ConflictException>();
        await _templateRepo.DidNotReceive().AddAsync(Arg.Any<Template>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenValidHtmlTemplate_ShouldUploadImmutableVersionKeyAndPersist()
    {
        // Arrange
        var projectId = TestConstants.DefaultProjectId;
        _templateRepo.SlugExistsAsync("tax-invoice", Arg.Any<CancellationToken>())
            .Returns(false);

        var command = new CreateTemplateCommand("Tax Invoice", "tax-invoice", "Finance");

        // Act
        TemplateResultDto result = await CreateSut().ExecuteAsync(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Name.Should().Be("Tax Invoice");
        result.Slug.Should().Be("tax-invoice");
        result.Category.Should().Be("Finance");
        result.ProjectId.Should().Be(projectId);
        result.IsActive.Should().BeTrue();

        var expectedKey = $"templates/{projectId:N}/{result.Id:N}/v1.html";
        await _storageService.Received(1).UploadAsync(
            StorageBuckets.Templates,
            expectedKey,
            Arg.Any<Stream>(),
            "text/html; charset=utf-8",
            Arg.Any<CancellationToken>());

        await _templateRepo.Received(1).AddAsync(
            Arg.Is<Template>(t =>
                t.Versions.Count == 1 &&
                t.Versions.Single().Version == 1 &&
                t.Versions.Single().StorageKey == expectedKey &&
                t.Versions.Single().MappingsSnapshot == "[]" &&
                t.Versions.Single().DataSchema == "{\"$schema\":\"mock-schema\"}" &&
                t.Versions.Single().SamplePayload == "{}"),
            Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenDocxFailsSecurityScan_ShouldThrowDomainValidationExceptionWithoutUploading()
    {
        // Arrange
        _templateRepo.SlugExistsAsync("unsafe-doc", Arg.Any<CancellationToken>()).Returns(false);
        _securityScanner.Scan(Arg.Any<Stream>())
            .Returns(new DocxScanResult(false, ["VBA macro project detected."]));

        using var fileStream = new MemoryStream(Encoding.UTF8.GetBytes("malicious-docx"));
        var command = new CreateTemplateCommand("Unsafe Doc", "unsafe-doc", "Security", fileStream, "unsafe.docx");

        // Act
        var act = () => CreateSut().ExecuteAsync(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<DomainValidationException>()
            .WithMessage("*VBA macro project detected*");
        await _storageService.DidNotReceive().UploadAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenDbCommitFails_ShouldExecuteCompensatingDeleteOnMinio()
    {
        // Arrange
        _templateRepo.SlugExistsAsync("tax-invoice", Arg.Any<CancellationToken>()).Returns(false);
        _unitOfWork.CommitAsync(Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("DB connection dropped"));

        var command = new CreateTemplateCommand("Tax Invoice", "tax-invoice", "Finance");

        // Act
        var act = () => CreateSut().ExecuteAsync(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("DB connection dropped");
        await _storageService.Received(1).DeleteAsync(
            StorageBuckets.Templates,
            Arg.Is<string>(k => k.StartsWith($"templates/{TestConstants.DefaultProjectId:N}/") && k.EndsWith("/v1.html")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenFileExceeds20Mb_ShouldThrowDomainValidationException()
    {
        // Arrange
        _templateRepo.SlugExistsAsync("giant-doc", Arg.Any<CancellationToken>()).Returns(false);
        var mockStream = Substitute.For<Stream>();
        mockStream.CanRead.Returns(true);
        mockStream.CanSeek.Returns(true);
        mockStream.Length.Returns(21L * 1024 * 1024);

        var command = new CreateTemplateCommand("Giant Doc", "giant-doc", "General", mockStream, "giant.html");

        // Act
        var act = () => CreateSut().ExecuteAsync(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<DomainValidationException>()
            .WithMessage("*exceeds the 20 MB limit*");
        await _storageService.DidNotReceive().UploadAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenFileExtensionUnsupported_ShouldThrowDomainValidationException()
    {
        // Arrange
        _templateRepo.SlugExistsAsync("invalid-ext", Arg.Any<CancellationToken>()).Returns(false);
        using var stream = new MemoryStream([1, 2, 3]);
        var command = new CreateTemplateCommand("Invalid Ext", "invalid-ext", "General", stream, "script.exe");

        // Act
        var act = () => CreateSut().ExecuteAsync(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<DomainValidationException>()
            .WithMessage("*Unsupported template file extension*");
        await _storageService.DidNotReceive().UploadAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }
}
