using FluentAssertions;
using Moq;
using SmkDoc.Application.UseCases.Templates.Commands.UpdateTemplateDetails;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Exceptions;
using SmkDoc.Domain.Interfaces;
using Xunit;

namespace SmkDoc.Tests.Application.UseCases.Templates;

public class UpdateTemplateDetailsUseCaseTests
{
    private readonly Mock<ITemplateRepository> _mockTemplateRepo = new();
    private readonly Mock<IUnitOfWork> _mockUow = new();
    private readonly UpdateTemplateDetailsCommandValidator _validator = new();

    private UpdateTemplateDetailsUseCase CreateUseCase() => new(
        _mockTemplateRepo.Object,
        _mockUow.Object,
        _validator);

    [Fact]
    public async Task ExecuteAsync_WhenTemplateNotFound_ShouldThrowNotFoundException()
    {
        var templateId = Guid.NewGuid();
        _mockTemplateRepo
            .Setup(r => r.GetByIdAsync(templateId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Template?)null);

        var useCase = CreateUseCase();
        var command = new UpdateTemplateDetailsCommand(templateId, "New Name", "New Category");

        var act = () => useCase.ExecuteAsync(command);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task ExecuteAsync_WhenValid_ShouldUpdateDetailsAndCommit()
    {
        var templateId = Guid.NewGuid();
        var template = new Template(Guid.NewGuid(), "Old Name", "slug", "Old Category") { Id = templateId };

        _mockTemplateRepo
            .Setup(r => r.GetByIdAsync(templateId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(template);

        var useCase = CreateUseCase();
        var command = new UpdateTemplateDetailsCommand(templateId, "Updated Name", "Updated Category");

        var response = await useCase.ExecuteAsync(command);

        template.Name.Should().Be("Updated Name");
        template.Category.Should().Be("Updated Category");
        response.Name.Should().Be("Updated Name");
        response.Category.Should().Be("Updated Category");

        _mockTemplateRepo.Verify(r => r.Update(template), Times.Once);
        _mockUow.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
