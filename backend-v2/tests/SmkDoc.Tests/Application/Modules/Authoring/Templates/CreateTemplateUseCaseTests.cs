using FluentAssertions;
using Moq;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.UseCases.Templates.Commands.CreateTemplate;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Exceptions;
using SmkDoc.Domain.Interfaces;
using Xunit;

namespace SmkDoc.Tests.Application.UseCases.Templates;

public class CreateTemplateUseCaseTests
{
    private readonly Mock<ITemplateRepository> _mockTemplateRepo = new();
    private readonly Mock<IRepository<TemplateVersion>> _mockVersionRepo = new();
    private readonly Mock<IStorageService> _mockStorage = new();
    private readonly Mock<IDocxSecurityScanner> _mockSecurity = new();
    private readonly Mock<IExecutionContext> _mockContext = new();
    private readonly Mock<IUnitOfWork> _mockUow = new();
    private readonly CreateTemplateCommandValidator _validator = new();

    private CreateTemplateUseCase CreateUseCase() => new(
        _mockTemplateRepo.Object,
        _mockVersionRepo.Object,
        _mockStorage.Object,
        _mockSecurity.Object,
        _mockContext.Object,
        _mockUow.Object,
        _validator);

    [Fact]
    public async Task ExecuteAsync_WhenCommandIsInvalid_ShouldFailFastWithoutTouchingDb()
    {
        var useCase = CreateUseCase();
        var invalidCommand = new CreateTemplateCommand(Guid.Empty, "", "INVALID_SLUG", null);

        var act = () => useCase.ExecuteAsync(invalidCommand);

        await act.Should().ThrowAsync<ValidationException>();

        _mockTemplateRepo.Verify(r => r.AddAsync(It.IsAny<Template>(), It.IsAny<CancellationToken>()), Times.Never);
        _mockUow.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_WhenSlugAlreadyExists_ShouldThrowConflictException()
    {
        var projectId = Guid.NewGuid();
        _mockTemplateRepo
            .Setup(r => r.SlugExistsAsync("tax-invoice", projectId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var useCase = CreateUseCase();
        var command = new CreateTemplateCommand(projectId, "Tax Invoice", "tax-invoice", "Finance");

        var act = () => useCase.ExecuteAsync(command);

        await act.Should().ThrowAsync<ConflictException>();
        _mockTemplateRepo.Verify(r => r.AddAsync(It.IsAny<Template>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_WhenValidHtmlTemplate_ShouldPersistAndReturnTemplateResponse()
    {
        var projectId = Guid.NewGuid();
        _mockTemplateRepo
            .Setup(r => r.SlugExistsAsync("tax-invoice", projectId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _mockContext.Setup(c => c.CallerApp).Returns("erp-system");

        var useCase = CreateUseCase();
        var command = new CreateTemplateCommand(projectId, "Tax Invoice", "tax-invoice", "Finance");

        var response = await useCase.ExecuteAsync(command);

        response.Should().NotBeNull();
        response.Name.Should().Be("Tax Invoice");
        response.Slug.Should().Be("tax-invoice");
        response.Category.Should().Be("Finance");
        response.ProjectId.Should().Be(projectId);
        response.IsActive.Should().BeTrue();

        _mockTemplateRepo.Verify(r => r.AddAsync(It.IsAny<Template>(), It.IsAny<CancellationToken>()), Times.Once);
        _mockVersionRepo.Verify(r => r.AddAsync(It.IsAny<TemplateVersion>(), It.IsAny<CancellationToken>()), Times.Once);
        _mockUow.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Exactly(3));
    }
}
