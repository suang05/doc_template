using FluentAssertions;
using Moq;
using SmkDoc.Application.Modules.Authoring.Templates.Queries.GetTemplateById;
using SmkDoc.Domain.Exceptions;
using SmkDoc.Tests.Common.Builders;
using SmkDoc.Tests.Common.Fixtures;
using Xunit;

namespace SmkDoc.Tests.Application.Modules.Authoring.Templates.Queries.GetTemplateById;

public class GetTemplateByIdUseCaseTests
{
    private readonly TemplateAuthoringTestFixture _fixture = new();

    private GetTemplateByIdUseCase CreateSut() => _fixture.BuildGetTemplateByIdUseCase();

    [Fact]
    public async Task ExecuteAsync_WhenTemplateNotFound_ShouldThrowNotFoundException()
    {
        var templateId = Guid.NewGuid();
        _fixture.TemplateRepo
            .Setup(r => r.GetByIdAsync(templateId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((SmkDoc.Domain.Entities.Template?)null);

        var act = () => CreateSut().ExecuteAsync(new GetTemplateByIdQuery(templateId));

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task ExecuteAsync_WhenTemplateExists_ShouldReturnMappedResponse()
    {
        var templateId = Guid.NewGuid();
        var template = new TemplateBuilder()
            .WithId(templateId)
            .WithName("Statement")
            .WithSlug("statement")
            .Build();

        _fixture.TemplateRepo
            .Setup(r => r.GetByIdAsync(templateId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(template);

        var response = await CreateSut().ExecuteAsync(new GetTemplateByIdQuery(templateId));

        response.Should().NotBeNull();
        response.Id.Should().Be(templateId);
        response.Name.Should().Be("Statement");
        response.Slug.Should().Be("statement");
    }
}
