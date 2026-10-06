using FluentAssertions;
using Moq;
using SmkDoc.Application.Modules.Authoring.Templates.Commands.UpdateTemplateDetails;
using SmkDoc.Domain.Exceptions;
using SmkDoc.Tests.Common.Builders;
using SmkDoc.Tests.Common.Fixtures;
using Xunit;

namespace SmkDoc.Tests.Application.Modules.Authoring.Templates;

public class UpdateTemplateDetailsUseCaseTests
{
    private readonly TemplateAuthoringTestFixture _fixture = new();

    [Fact]
    public async Task ExecuteAsync_WhenTemplateNotFound_ShouldThrowNotFoundException()
    {
        var templateId = Guid.NewGuid();
        _fixture.TemplateRepo
            .Setup(r => r.GetByIdAsync(templateId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((SmkDoc.Domain.Entities.Template?)null);

        var useCase = _fixture.BuildUpdateTemplateDetailsUseCase();
        var command = new UpdateTemplateDetailsCommand(templateId, "New Name", "New Category");

        var act = () => useCase.ExecuteAsync(command);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task ExecuteAsync_WhenValid_ShouldUpdateDetailsAndCommit()
    {
        var templateId = Guid.NewGuid();
        var template = new TemplateBuilder()
            .WithId(templateId)
            .WithName("Old Name")
            .WithSlug("slug")
            .Build();

        _fixture.TemplateRepo
            .Setup(r => r.GetByIdAsync(templateId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(template);

        var useCase = _fixture.BuildUpdateTemplateDetailsUseCase();
        var command = new UpdateTemplateDetailsCommand(templateId, "Updated Name", "Updated Category");

        var response = await useCase.ExecuteAsync(command);

        template.Name.Value.Should().Be("Updated Name");
        template.Category.Should().Be("Updated Category");
        response.Name.Should().Be("Updated Name");
        response.Category.Should().Be("Updated Category");

        _fixture.TemplateRepo.Verify(r => r.Update(template), Times.Once);
        _fixture.Uow.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
