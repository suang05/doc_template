using FluentAssertions;
using Moq;
using SmkDoc.Application.Modules.Authoring.Templates.Commands.CreateTemplate;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Exceptions;
using SmkDoc.Tests.Common.Fixtures;
using Xunit;

namespace SmkDoc.Tests.Application.Modules.Authoring.Templates;

public class CreateTemplateUseCaseTests
{
    private readonly TemplateAuthoringTestFixture _fixture = new();

    [Fact]
    public async Task ExecuteAsync_WhenCommandIsInvalid_ShouldFailFastWithoutTouchingDb()
    {
        var useCase = _fixture.BuildCreateTemplateUseCase();
        var invalidCommand = new CreateTemplateCommand(Guid.Empty, "", "INVALID_SLUG", null);

        var act = () => useCase.ExecuteAsync(invalidCommand);

        await act.Should().ThrowAsync<ValidationException>();

        _fixture.TemplateRepo.Verify(r => r.AddAsync(It.IsAny<Template>(), It.IsAny<CancellationToken>()), Times.Never);
        _fixture.Uow.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_WhenSlugAlreadyExists_ShouldThrowConflictException()
    {
        var projectId = Guid.NewGuid();
        _fixture.TemplateRepo
            .Setup(r => r.SlugExistsAsync("tax-invoice", projectId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var useCase = _fixture.BuildCreateTemplateUseCase();
        var command = new CreateTemplateCommand(projectId, "Tax Invoice", "tax-invoice", "Finance");

        var act = () => useCase.ExecuteAsync(command);

        await act.Should().ThrowAsync<ConflictException>();
        _fixture.TemplateRepo.Verify(r => r.AddAsync(It.IsAny<Template>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_WhenValidHtmlTemplate_ShouldPersistAndReturnTemplateResultDto()
    {
        var projectId = Guid.NewGuid();
        _fixture.TemplateRepo
            .Setup(r => r.SlugExistsAsync("tax-invoice", projectId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _fixture.Context.Setup(c => c.CallerApp).Returns("erp-system");

        var useCase = _fixture.BuildCreateTemplateUseCase();
        var command = new CreateTemplateCommand(projectId, "Tax Invoice", "tax-invoice", "Finance");

        var response = await useCase.ExecuteAsync(command);

        response.Should().NotBeNull();
        response.Name.Should().Be("Tax Invoice");
        response.Slug.Should().Be("tax-invoice");
        response.Category.Should().Be("Finance");
        response.ProjectId.Should().Be(projectId);
        response.IsActive.Should().BeTrue();

        _fixture.TemplateRepo.Verify(r => r.AddAsync(It.IsAny<Template>(), It.IsAny<CancellationToken>()), Times.Once);
        _fixture.VersionRepo.Verify(r => r.AddAsync(It.IsAny<TemplateVersion>(), It.IsAny<CancellationToken>()), Times.Once);
        _fixture.Uow.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Exactly(3));
    }
}
