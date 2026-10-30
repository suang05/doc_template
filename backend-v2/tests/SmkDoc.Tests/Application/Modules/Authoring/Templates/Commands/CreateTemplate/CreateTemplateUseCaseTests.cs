using System.Text;
using SmkDoc.Application.Common;
using SmkDoc.Application.Common.Exceptions;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.Authoring.Templates.Commands.CreateTemplate;
using SmkDoc.Application.Modules.Authoring.Templates.DTOs;
using SmkDoc.Application.Modules.Rendering.Documents.DTOs;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Exceptions;
using SmkDoc.Domain.Interfaces;
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
    private readonly CreateTemplateCommandValidator _validator = new();
    private readonly FakeTimeProvider _timeProvider = TestConstants.CreateFakeClock();

    public CreateTemplateUseCaseTests()
    {
        _projectContext.IsResolved.Returns(true);
        _projectContext.ProjectId.Returns(new ProjectId(TestConstants.DefaultProjectId));
        _projectContext.CallerIdentity.Returns("erp-system");
        _securityScanner.Scan(Arg.Any<Stream>()).Returns(new DocxScanResult(true, []));
        _unitOfWork.CommitAsync(Arg.Any<CancellationToken>()).Returns(1);
    }

    private CreateTemplateUseCase CreateSut() => new(
        _templateRepo,
        _storageService,
        _securityScanner,
        _projectContext,
        _unitOfWork,
        _validator,
        _timeProvider);

    [Fact]
    public async Task ExecuteAsync_WhenCommandIsInvalid_ShouldFailFastWithoutTouchingDb()
    {
        // Arrange
        var invalidCommand = new CreateTemplateCommand("", "INVALID_SLUG", null);

        // Act
        var act = () => CreateSut().ExecuteAsync(invalidCommand);

        // Assert
        await act.Should().ThrowAsync<ValidationException>();
        await _templateRepo.DidNotReceive().AddAsync(Arg.Any<Template>(), Arg.Any<CancellationToken>());
        await _unitOfWork.DidNotReceive().CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenProjectContextUnresolved_ShouldThrowForbiddenException()
    {
        // Arrange
        _projectContext.IsResolved.Returns(false);
        var command = new CreateTemplateCommand("Tax Invoice", "tax-invoice", "Finance");

        // Act
        var act = () => CreateSut().ExecuteAsync(command);

        // Assert
        await act.Should().ThrowAsync<ForbiddenException>()
            .WithMessage("*active project context*");
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
        var act = () => CreateSut().ExecuteAsync(command);

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
        TemplateResultDto result = await CreateSut().ExecuteAsync(command);

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
                t.Versions.Single().StorageKey == expectedKey),
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
        var act = () => CreateSut().ExecuteAsync(command);

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
        var act = () => CreateSut().ExecuteAsync(command);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("DB connection dropped");
        await _storageService.Received(1).DeleteAsync(
            StorageBuckets.Templates,
            Arg.Is<string>(k => k.StartsWith($"templates/{TestConstants.DefaultProjectId:N}/") && k.EndsWith("/v1.html")),
            Arg.Any<CancellationToken>());
    }
}
