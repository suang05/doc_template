using FluentAssertions;
using Moq;
using SmkDoc.Application.Modules.Authoring.Templates.Queries.ListTemplates;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Exceptions;
using SmkDoc.Domain.Interfaces;
using SmkDoc.Tests.Common.Builders;
using Xunit;

namespace SmkDoc.Tests.Application.Modules.Authoring.Templates.Queries.ListTemplates;

public class ListTemplatesUseCaseTests
{
    private readonly Mock<ITemplateRepository> _mockRepo = new();

    private ListTemplatesUseCase CreateSut() => new(_mockRepo.Object);

    [Fact]
    public async Task ExecuteAsync_WhenEmptyProjectId_ShouldThrowDomainValidationException()
    {
        var act = () => CreateSut().ExecuteAsync(new ListTemplatesQuery(Guid.Empty));

        await act.Should().ThrowAsync<DomainValidationException>()
            .WithMessage("*ProjectId cannot be empty*");
    }

    [Fact]
    public async Task ExecuteAsync_WhenValidProjectId_ShouldReturnProjectScopedTemplates()
    {
        var projectId = Guid.NewGuid();
        var template1 = new TemplateBuilder()
            .WithProjectId(projectId)
            .WithName("Invoice")
            .WithSlug("invoice")
            .Build();
        var template2 = new TemplateBuilder()
            .WithProjectId(projectId)
            .WithName("Receipt")
            .WithSlug("receipt")
            .Build();

        _mockRepo.Setup(r => r.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Template> { template1, template2 });

        var result = await CreateSut().ExecuteAsync(new ListTemplatesQuery(projectId));

        result.Should().HaveCount(2);
        result[0].Name.Should().Be("Invoice");
        result[1].Name.Should().Be("Receipt");
        _mockRepo.Verify(r => r.ListAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_WhenProjectIdEmptyButAmbientContextResolved_ShouldReturnScopedTemplates()
    {
        var projectId = Guid.NewGuid();
        var template = new TemplateBuilder()
            .WithProjectId(projectId)
            .WithName("Invoice")
            .WithSlug("invoice")
            .Build();

        var mockContext = new Mock<SmkDoc.Application.Common.Interfaces.ICurrentProjectContext>();
        mockContext.Setup(c => c.IsResolved).Returns(true);
        mockContext.Setup(c => c.ProjectId).Returns(new SmkDoc.Domain.ValueObjects.Ids.ProjectId(projectId));

        _mockRepo.Setup(r => r.ListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Template> { template });

        var sut = new ListTemplatesUseCase(_mockRepo.Object, mockContext.Object);
        var result = await sut.ExecuteAsync(new ListTemplatesQuery(Guid.Empty));

        result.Should().HaveCount(1);
        result[0].Name.Should().Be("Invoice");
    }
}

