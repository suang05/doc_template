using SmkDoc.Application.Modules.Authoring.Templates.DTOs;
using SmkDoc.Application.Modules.Authoring.Templates.Queries.GetTemplateById;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Exceptions;
using SmkDoc.Domain.Interfaces;
using SmkDoc.Tests.Common.Builders;

namespace SmkDoc.Tests.Application.Modules.Authoring.Templates.Queries.GetTemplateById;

public sealed class GetTemplateByIdUseCaseTests
{
    private readonly ITemplateRepository _templateRepo = Substitute.For<ITemplateRepository>();

    private GetTemplateByIdUseCase CreateSut() => new(_templateRepo);

    [Fact]
    public async Task ExecuteAsync_WhenTemplateNotFound_ShouldThrowNotFoundException()
    {
        // Arrange
        var templateId = Guid.NewGuid();
        _templateRepo.GetByIdAsync(templateId, Arg.Any<CancellationToken>())
            .Returns((Template?)null);

        // Act
        var act = () => CreateSut().ExecuteAsync(new GetTemplateByIdQuery(templateId));

        // Assert
        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task ExecuteAsync_WhenTemplateExists_ShouldReturnMappedResponse()
    {
        // Arrange
        var templateId = Guid.NewGuid();
        Template template = TemplateBuilder.ATemplate()
            .WithId(templateId)
            .WithName("Statement")
            .WithSlug("statement");

        _templateRepo.GetByIdAsync(templateId, Arg.Any<CancellationToken>())
            .Returns(template);

        // Act
        TemplateResultDto result = await CreateSut().ExecuteAsync(new GetTemplateByIdQuery(templateId));

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be(templateId);
        result.Name.Should().Be("Statement");
        result.Slug.Should().Be("statement");
    }
}
