using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.Authoring.Templates.DTOs;
using SmkDoc.Application.Modules.Authoring.Templates.Queries.ListTemplates;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Exceptions;
using SmkDoc.Domain.Interfaces;
using SmkDoc.Domain.ValueObjects.Ids;
using SmkDoc.Tests.Common.Builders;

namespace SmkDoc.Tests.Application.Modules.Authoring.Templates.Queries.ListTemplates;

public sealed class ListTemplatesUseCaseTests
{
    private readonly ITemplateRepository _templateRepo = Substitute.For<ITemplateRepository>();
    private readonly ICurrentProjectContext _projectContext = Substitute.For<ICurrentProjectContext>();

    private ListTemplatesUseCase CreateSut(ICurrentProjectContext? projectContext = null) =>
        new(_templateRepo, projectContext);

    [Fact]
    public async Task ExecuteAsync_WhenEmptyProjectId_ShouldThrowDomainValidationException()
    {
        // Act
        var act = () => CreateSut().ExecuteAsync(new ListTemplatesQuery(Guid.Empty));

        // Assert
        await act.Should().ThrowAsync<DomainValidationException>()
            .WithMessage("*ProjectId cannot be empty*");
    }

    [Fact]
    public async Task ExecuteAsync_WhenValidProjectId_ShouldReturnProjectScopedTemplates()
    {
        // Arrange
        var projectId = Guid.NewGuid();
        Template template1 = TemplateBuilder.ATemplate()
            .WithProjectId(projectId)
            .WithName("Invoice")
            .WithSlug("invoice");
        Template template2 = TemplateBuilder.ATemplate()
            .WithProjectId(projectId)
            .WithName("Receipt")
            .WithSlug("receipt");

        _templateRepo.ListAsync(Arg.Any<CancellationToken>())
            .Returns(new List<Template> { template1, template2 });

        // Act
        IReadOnlyList<TemplateResultDto> result = await CreateSut().ExecuteAsync(new ListTemplatesQuery(projectId));

        // Assert
        result.Should().HaveCount(2);
        result[0].Name.Should().Be("Invoice");
        result[1].Name.Should().Be("Receipt");
        await _templateRepo.Received(1).ListAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenProjectIdEmptyButAmbientContextResolved_ShouldReturnScopedTemplates()
    {
        // Arrange
        var projectId = Guid.NewGuid();
        Template template = TemplateBuilder.ATemplate()
            .WithProjectId(projectId)
            .WithName("Invoice")
            .WithSlug("invoice");

        _projectContext.IsResolved.Returns(true);
        _projectContext.ProjectId.Returns(new ProjectId(projectId));

        _templateRepo.ListAsync(Arg.Any<CancellationToken>())
            .Returns(new List<Template> { template });

        // Act
        var sut = CreateSut(_projectContext);
        IReadOnlyList<TemplateResultDto> result = await sut.ExecuteAsync(new ListTemplatesQuery(Guid.Empty));

        // Assert
        result.Should().HaveCount(1);
        result[0].Name.Should().Be("Invoice");
    }
}
