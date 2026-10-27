using FluentAssertions;
using NSubstitute;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.Authoring.Templates.DTOs;
using SmkDoc.Application.Modules.Authoring.Templates.Queries.ListTemplates;
using SmkDoc.Domain.Exceptions;
using SmkDoc.Domain.ValueObjects.Ids;
using Xunit;

namespace SmkDoc.Tests.Application.Modules.Authoring.Templates.Queries.ListTemplates;

public sealed class ListTemplatesUseCaseTests
{
    private readonly ITemplateQueryService _templateQueryService = Substitute.For<ITemplateQueryService>();
    private readonly ICurrentProjectContext _projectContext = Substitute.For<ICurrentProjectContext>();

    private ListTemplatesUseCase CreateSut(ICurrentProjectContext? projectContext = null) =>
        new(_templateQueryService, projectContext);

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
        var template1 = new TemplateResultDto(Guid.NewGuid(), projectId, "Invoice", "invoice", "Billing", true, null, null, DateTimeOffset.UtcNow, null);
        var template2 = new TemplateResultDto(Guid.NewGuid(), projectId, "Receipt", "receipt", "Billing", true, null, null, DateTimeOffset.UtcNow, null);

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
        var result = await CreateSut().ExecuteAsync(new ListTemplatesQuery(projectId));

        // Assert
        result.Items.Should().HaveCount(2);
        result.TotalCount.Should().Be(2);
        result.Items[0].Name.Should().Be("Invoice");
        result.Items[1].Name.Should().Be("Receipt");
    }

    [Fact]
    public async Task ExecuteAsync_WhenProjectIdEmptyButAmbientContextResolved_ShouldReturnScopedTemplates()
    {
        // Arrange
        var projectId = Guid.NewGuid();
        var template = new TemplateResultDto(Guid.NewGuid(), projectId, "Invoice", "invoice", "Billing", true, null, null, DateTimeOffset.UtcNow, null);

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
            .Returns(([template], 1));

        // Act
        var sut = CreateSut(_projectContext);
        var result = await sut.ExecuteAsync(new ListTemplatesQuery(Guid.Empty));

        // Assert
        result.Items.Should().HaveCount(1);
        result.TotalCount.Should().Be(1);
        result.Items[0].Name.Should().Be("Invoice");
    }

    [Fact]
    public async Task ExecuteAsync_WhenQueryProjectIdMismatchesAmbientContext_ShouldThrowForbiddenException()
    {
        // Arrange
        var contextProjectId = Guid.NewGuid();
        var spoofedProjectId = Guid.NewGuid();

        _projectContext.IsResolved.Returns(true);
        _projectContext.ProjectId.Returns(new ProjectId(contextProjectId));

        var sut = CreateSut(_projectContext);

        // Act
        var act = () => sut.ExecuteAsync(new ListTemplatesQuery(spoofedProjectId));

        // Assert
        await act.Should().ThrowAsync<ForbiddenException>()
            .WithMessage("*Cross-tenant template listing rejected*");
        await _templateQueryService.DidNotReceive().ListPagedAsync(
            Arg.Any<ProjectId>(),
            Arg.Any<string?>(),
            Arg.Any<string?>(),
            Arg.Any<bool?>(),
            Arg.Any<int>(),
            Arg.Any<int>(),
            Arg.Any<CancellationToken>());
    }
}
