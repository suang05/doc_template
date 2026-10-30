using FluentAssertions;
using NSubstitute;
using SmkDoc.Application.Common.Exceptions;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.Authoring.Templates.DTOs;
using SmkDoc.Application.Modules.Authoring.Templates.Queries.ListTemplates;
using SmkDoc.Domain.ValueObjects.Ids;
using Xunit;

namespace SmkDoc.Tests.Application.Modules.Authoring.Templates.Queries.ListTemplates;

public sealed class ListTemplatesUseCaseTests
{
    private readonly ITemplateQueryService _templateQueryService = Substitute.For<ITemplateQueryService>();
    private readonly ICurrentProjectContext _projectContext = Substitute.For<ICurrentProjectContext>();

    private ListTemplatesUseCase CreateSut() =>
        new(_templateQueryService, _projectContext);

    [Fact]
    public async Task ExecuteAsync_WhenProjectContextUnresolved_ShouldThrowForbiddenException()
    {
        // Arrange
        _projectContext.IsResolved.Returns(false);

        // Act
        var act = () => CreateSut().ExecuteAsync(new ListTemplatesQuery());

        // Assert
        await act.Should().ThrowAsync<ForbiddenException>()
            .WithMessage("*Active project context is required*");
        await _templateQueryService.DidNotReceive().ListPagedAsync(
            Arg.Any<ProjectId>(),
            Arg.Any<string?>(),
            Arg.Any<string?>(),
            Arg.Any<bool?>(),
            Arg.Any<int>(),
            Arg.Any<int>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenAmbientContextResolved_ShouldReturnProjectScopedTemplates()
    {
        // Arrange
        var projectId = Guid.NewGuid();
        var template1 = new TemplateResultDto(Guid.NewGuid(), projectId, "Invoice", "invoice", "Billing", true, null, null, DateTimeOffset.UtcNow, null);
        var template2 = new TemplateResultDto(Guid.NewGuid(), projectId, "Receipt", "receipt", "Billing", true, null, null, DateTimeOffset.UtcNow, null);

        _projectContext.IsResolved.Returns(true);
        _projectContext.ProjectId.Returns(new ProjectId(projectId));

        _templateQueryService.ListPagedAsync(
            new ProjectId(projectId),
            Arg.Any<string?>(),
            Arg.Any<string?>(),
            Arg.Any<bool?>(),
            Arg.Any<int>(),
            Arg.Any<int>(),
            Arg.Any<CancellationToken>())
            .Returns(([template1, template2], 2));

        // Act
        var result = await CreateSut().ExecuteAsync(new ListTemplatesQuery(SearchTerm: "inv", Category: "Billing", IsActive: true, Page: 1, PageSize: 20));

        // Assert
        result.Items.Should().HaveCount(2);
        result.TotalCount.Should().Be(2);
        result.Items[0].Name.Should().Be("Invoice");
        result.Items[1].Name.Should().Be("Receipt");
    }
}
