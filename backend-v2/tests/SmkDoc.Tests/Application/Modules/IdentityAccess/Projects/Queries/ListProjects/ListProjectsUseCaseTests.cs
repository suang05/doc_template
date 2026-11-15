using SmkDoc.Application.Modules.IdentityAccess.Projects.DTOs;
using SmkDoc.Application.Modules.IdentityAccess.Projects.Queries.ListProjects;
using SmkDoc.Domain.ValueObjects.Ids;

namespace SmkDoc.Tests.Application.Modules.IdentityAccess.Projects.Queries.ListProjects;

public sealed class ListProjectsUseCaseTests
{
    private readonly IProjectQueryService _projectQueryService = Substitute.For<IProjectQueryService>();
    private readonly FakeTimeProvider _timeProvider = TestConstants.CreateFakeClock();

    private ListProjectsUseCase CreateSut() => new(_projectQueryService, _timeProvider);

    [Fact]
    public async Task ExecuteAsync_QueriesAccessibleProjectsWithCurrentTimestamp()
    {
        // Arrange
        var now = _timeProvider.GetUtcNow();
        var userId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var projectDto = new ProjectResultDto(projectId, "Proj 1", "proj-1", true, now);

        _projectQueryService.ListAccessiblePagedAsync(
                new UserId(userId),
                now,
                Arg.Any<string?>(),
                1,
                20,
                false,
                Arg.Any<CancellationToken>())
            .Returns(([projectDto], (long?)null));

        // Act
        ProjectPagedResultDto result = await CreateSut().ExecuteAsync(new ListProjectsQuery(userId), CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.TotalCount.Should().BeNull();
        result.Items.Should().HaveCount(1);
        result.Items[0].Id.Should().Be(projectId);
        result.Items[0].Name.Should().Be("Proj 1");

        await _projectQueryService.Received(1).ListAccessiblePagedAsync(
            new UserId(userId),
            now,
            null,
            1,
            20,
            false,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WithSearchAndPaging_ForwardsParametersToQueryService()
    {
        // Arrange
        var now = _timeProvider.GetUtcNow();
        var userId = Guid.NewGuid();

        _projectQueryService.ListAccessiblePagedAsync(
                new UserId(userId),
                now,
                "invoice",
                2,
                10,
                true,
                Arg.Any<CancellationToken>())
            .Returns(([], (long?)0L));

        // Act
        var query = new ListProjectsQuery(userId, "invoice", 2, 10, IncludeTotal: true);
        ProjectPagedResultDto result = await CreateSut().ExecuteAsync(query, CancellationToken.None);

        // Assert
        result.Page.Should().Be(2);
        result.Limit.Should().Be(10);
        result.TotalCount.Should().Be(0L);
        result.Items.Should().BeEmpty();
    }

    [Theory]
    [InlineData(1, 20, false)]
    [InlineData(4, 25, true)]
    public async Task ExecuteAsync_ForwardsPageLimitAndIncludeTotal(
        int page,
        int limit,
        bool includeTotal)
    {
        // Arrange
        var now = _timeProvider.GetUtcNow();
        var userId = Guid.NewGuid();
        long? totalCount = includeTotal ? 12L : null;

        _projectQueryService.ListAccessiblePagedAsync(
                new UserId(userId),
                now,
                null,
                page,
                limit,
                includeTotal,
                Arg.Any<CancellationToken>())
            .Returns(([], totalCount));

        // Act
        ProjectPagedResultDto result = await CreateSut().ExecuteAsync(
            new ListProjectsQuery(userId, null, page, limit, includeTotal), CancellationToken.None);

        // Assert
        result.Page.Should().Be(page);
        result.Limit.Should().Be(limit);
        result.TotalCount.Should().Be(totalCount);
        await _projectQueryService.Received(1).ListAccessiblePagedAsync(
            new UserId(userId),
            now,
            null,
            page,
            limit,
            includeTotal,
            Arg.Any<CancellationToken>());
    }
}
