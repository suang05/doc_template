using FluentAssertions;
using Moq;
using SmkDoc.Application.Modules.Authoring.Templates.Queries.GetTemplateById;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Exceptions;
using SmkDoc.Domain.Interfaces;
using Xunit;

namespace SmkDoc.Tests.Application.UseCases.Templates;

public class GetTemplateByIdUseCaseTests
{
    private readonly Mock<ITemplateRepository> _mockTemplateRepo = new();

    [Fact]
    public async Task ExecuteAsync_WhenTemplateNotFound_ShouldThrowNotFoundException()
    {
        var templateId = Guid.NewGuid();
        _mockTemplateRepo
            .Setup(r => r.GetByIdAsync(templateId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Template?)null);

        var useCase = new GetTemplateByIdUseCase(_mockTemplateRepo.Object);
        var act = () => useCase.ExecuteAsync(new GetTemplateByIdQuery(templateId));

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task ExecuteAsync_WhenTemplateExists_ShouldReturnMappedResponse()
    {
        var templateId = Guid.NewGuid();
        var template = new Template(Guid.NewGuid(), "Statement", "statement", "Accounting") { Id = templateId };

        _mockTemplateRepo
            .Setup(r => r.GetByIdAsync(templateId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(template);

        var useCase = new GetTemplateByIdUseCase(_mockTemplateRepo.Object);
        var response = await useCase.ExecuteAsync(new GetTemplateByIdQuery(templateId));

        response.Should().NotBeNull();
        response.Id.Should().Be(templateId);
        response.Name.Should().Be("Statement");
        response.Slug.Should().Be("statement");
        response.Category.Should().Be("Accounting");
    }
}
