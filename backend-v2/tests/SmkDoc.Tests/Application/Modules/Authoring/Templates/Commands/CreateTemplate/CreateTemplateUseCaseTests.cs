using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.Authoring.Templates.Commands.CreateTemplate;
using SmkDoc.Application.Modules.Authoring.Templates.DTOs;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Exceptions;
using SmkDoc.Domain.Interfaces;
using SmkDoc.Domain.ValueObjects;
using SmkDoc.Domain.ValueObjects.Ids;
using ValidationException = SmkDoc.Application.Common.Exceptions.ValidationException;

namespace SmkDoc.Tests.Application.Modules.Authoring.Templates.Commands.CreateTemplate;

public sealed class CreateTemplateUseCaseTests
{
    private readonly ITemplateRepository _templateRepo = Substitute.For<ITemplateRepository>();
    private readonly IStorageService _storageService = Substitute.For<IStorageService>();
    private readonly IDocxSecurityScanner _securityScanner = Substitute.For<IDocxSecurityScanner>();
    private readonly IExecutionContext _executionContext = Substitute.For<IExecutionContext>();
    private readonly ICurrentProjectContext _projectContext = Substitute.For<ICurrentProjectContext>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly CreateTemplateCommandValidator _validator = new();
    private readonly FakeTimeProvider _timeProvider = TestConstants.CreateFakeClock();

    public CreateTemplateUseCaseTests()
    {
        _executionContext.UserId.Returns(TestConstants.DefaultUserId);
        _projectContext.IsResolved.Returns(true);
        _projectContext.ProjectId.Returns(new ProjectId(TestConstants.DefaultProjectId));
        _unitOfWork.CommitAsync(Arg.Any<CancellationToken>()).Returns(1);
    }

    private CreateTemplateUseCase CreateSut() => new(
        _templateRepo,
        _storageService,
        _securityScanner,
        _projectContext,
        _executionContext,
        _unitOfWork,
        _validator,
        _timeProvider);

    [Fact]
    public async Task ExecuteAsync_WhenCommandIsInvalid_ShouldFailFastWithoutTouchingDb()
    {
        // Arrange
        var invalidCommand = new CreateTemplateCommand(Guid.Empty, "", "INVALID_SLUG", null);

        // Act
        var act = () => CreateSut().ExecuteAsync(invalidCommand);

        // Assert
        await act.Should().ThrowAsync<ValidationException>();
        await _templateRepo.DidNotReceive().AddAsync(Arg.Any<Template>(), Arg.Any<CancellationToken>());
        await _unitOfWork.DidNotReceive().CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenSlugAlreadyExists_ShouldThrowConflictException()
    {
        // Arrange
        var projectId = Guid.NewGuid();
        _templateRepo.SlugExistsAsync("tax-invoice", Arg.Any<CancellationToken>())
            .Returns(true);

        var command = new CreateTemplateCommand(projectId, "Tax Invoice", "tax-invoice", "Finance");

        // Act
        var act = () => CreateSut().ExecuteAsync(command);

        // Assert
        await act.Should().ThrowAsync<ConflictException>();
        await _templateRepo.DidNotReceive().AddAsync(Arg.Any<Template>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenValidHtmlTemplate_ShouldPersistAndReturnTemplateResultDto()
    {
        // Arrange
        var projectId = Guid.NewGuid();
        _templateRepo.SlugExistsAsync("tax-invoice", Arg.Any<CancellationToken>())
            .Returns(false);
        _executionContext.CallerApp.Returns("erp-system");

        var command = new CreateTemplateCommand(projectId, "Tax Invoice", "tax-invoice", "Finance");

        // Act
        TemplateResultDto result = await CreateSut().ExecuteAsync(command);

        // Assert
        result.Should().NotBeNull();
        result.Name.Should().Be("Tax Invoice");
        result.Slug.Should().Be("tax-invoice");
        result.Category.Should().Be("Finance");
        result.ProjectId.Should().Be(projectId);
        result.IsActive.Should().BeTrue();

        await _templateRepo.Received(1).AddAsync(
            Arg.Is<Template>(t => t.Versions.Count == 1 && t.Versions.Any(v => v.Version == 1)),
            Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).CommitAsync(Arg.Any<CancellationToken>());
    }
}
