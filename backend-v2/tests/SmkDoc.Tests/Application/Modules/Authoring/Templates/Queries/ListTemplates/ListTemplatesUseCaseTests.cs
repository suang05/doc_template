using SmkDoc.Application.Modules.Authoring.Templates.DTOs;
using SmkDoc.Application.Modules.Authoring.Templates.Queries.ListTemplates;

namespace SmkDoc.Tests.Application.Modules.Authoring.Templates.Queries.ListTemplates;

public sealed class ListTemplatesUseCaseTests
{
    private readonly ITemplateQueryService _templateQueryService = Substitute.For<ITemplateQueryService>();
    private ListTemplatesUseCase CreateSut() =>
        new(_templateQueryService);

    [Fact]
    public async Task ExecuteAsync_WhenAmbientContextResolved_ShouldReturnProjectScopedTemplates()
    {
        // Arrange
        var projectId = Guid.NewGuid();
        var template1 = new TemplateResultDto(Guid.NewGuid(), projectId, "Invoice", "invoice", "Billing", true, null, null, DateTimeOffset.UtcNow, null);
        var template2 = new TemplateResultDto(Guid.NewGuid(), projectId, "Receipt", "receipt", "Billing", true, null, null, DateTimeOffset.UtcNow, null);

        _templateQueryService.ListPagedAsync(
            Arg.Any<string?>(),
            Arg.Any<string?>(),
            Arg.Any<bool?>(),
            Arg.Any<int>(),
            Arg.Any<int>(),
            Arg.Any<CancellationToken>())
            .Returns(([template1, template2], 2));

        // Act
        var result = await CreateSut().ExecuteAsync(new ListTemplatesQuery(SearchTerm: "inv", Category: "Billing", IsActive: true, Page: 1, PageSize: 20), CancellationToken.None);

        // Assert
        result.Items.Should().HaveCount(2);
        result.TotalCount.Should().Be(2);
        result.Items[0].Name.Should().Be("Invoice");
        result.Items[1].Name.Should().Be("Receipt");
    }

    [Theory]
    [InlineData(0, 50000, 1, 100)]
    [InlineData(-5, 0, 1, 1)]
    [InlineData(3, 50, 3, 50)]
    public async Task ExecuteAsync_WithOutOfBoundsPagination_ClampsPageAndPageSize(
        int requestedPage,
        int requestedPageSize,
        int expectedPage,
        int expectedPageSize)
    {
        // Arrange
        _templateQueryService.ListPagedAsync(
            Arg.Any<string?>(),
            Arg.Any<string?>(),
            Arg.Any<bool?>(),
            expectedPage,
            expectedPageSize,
            Arg.Any<CancellationToken>())
            .Returns(([], 0));

        // Act
        var result = await CreateSut().ExecuteAsync(
            new ListTemplatesQuery(Page: requestedPage, PageSize: requestedPageSize), CancellationToken.None);

        // Assert
        result.Page.Should().Be(expectedPage);
        result.PageSize.Should().Be(expectedPageSize);
        await _templateQueryService.Received(1).ListPagedAsync(
            null,
            null,
            null,
            expectedPage,
            expectedPageSize,
            Arg.Any<CancellationToken>());
    }
}
