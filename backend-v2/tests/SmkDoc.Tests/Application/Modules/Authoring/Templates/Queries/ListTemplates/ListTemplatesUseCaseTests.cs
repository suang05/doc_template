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
            Arg.Any<bool>(),
            Arg.Any<CancellationToken>())
            .Returns(([template1, template2], (long?)2L));

        // Act
        var result = await CreateSut().ExecuteAsync(
            new ListTemplatesQuery(SearchTerm: "inv", Category: "Billing", IsActive: true, Page: 1, Limit: 20, IncludeTotal: true),
            CancellationToken.None);

        // Assert
        result.Items.Should().HaveCount(2);
        result.TotalCount.Should().Be(2L);
        result.Items[0].Name.Should().Be("Invoice");
        result.Items[1].Name.Should().Be("Receipt");
    }

    [Theory]
    [InlineData(1, 20, false)]
    [InlineData(3, 50, true)]
    public async Task ExecuteAsync_ForwardsPaginationAndIncludeTotalToQueryService(
        int page,
        int limit,
        bool includeTotal)
    {
        // Arrange
        long? totalCount = includeTotal ? 15L : null;
        _templateQueryService.ListPagedAsync(
            Arg.Any<string?>(),
            Arg.Any<string?>(),
            Arg.Any<bool?>(),
            page,
            limit,
            includeTotal,
            Arg.Any<CancellationToken>())
            .Returns(([], totalCount));

        // Act
        var result = await CreateSut().ExecuteAsync(
            new ListTemplatesQuery(Page: page, Limit: limit, IncludeTotal: includeTotal), CancellationToken.None);

        // Assert
        result.Page.Should().Be(page);
        result.Limit.Should().Be(limit);
        result.TotalCount.Should().Be(totalCount);
        await _templateQueryService.Received(1).ListPagedAsync(
            null,
            null,
            null,
            page,
            limit,
            includeTotal,
            Arg.Any<CancellationToken>());
    }
}
