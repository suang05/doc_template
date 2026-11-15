using SmkDoc.Application.Modules.TenantManagement.BreakGlass.Queries.GetBreakGlassGrantById;
using SmkDoc.Domain.TenantManagement;
using SmkDoc.Domain.ValueObjects.Ids;

namespace SmkDoc.Tests.Application.Modules.TenantManagement.BreakGlass.Queries.GetBreakGlassGrantById;

public sealed class GetBreakGlassGrantByIdUseCaseTests
{
    private readonly IBreakGlassGrantRepository _grantRepo = Substitute.For<IBreakGlassGrantRepository>();
    private readonly FakeTimeProvider _timeProvider = TestConstants.CreateFakeClock();

    private GetBreakGlassGrantByIdUseCase CreateSut() => new(_grantRepo, _timeProvider);

    [Fact]
    public async Task ExecuteAsync_WhenGrantExists_ReturnsDtoWithVersionAndActiveStatus()
    {
        // Arrange
        var now = _timeProvider.GetUtcNow();
        var grant = BreakGlassGrant.Issue(
            ProjectId.New(),
            UserId.New(),
            UserId.New(),
            "Incident diagnostics",
            now,
            now.AddMinutes(30));

        _grantRepo.GetByIdAsync(grant.Id, Arg.Any<CancellationToken>())
            .Returns(grant);

        // Act
        var result = await CreateSut().ExecuteAsync(new GetBreakGlassGrantByIdQuery(grant.Id.Value), CancellationToken.None);

        // Assert
        result.GrantId.Should().Be(grant.Id.Value);
        result.Reason.Should().Be("Incident diagnostics");
        result.IsActive.Should().BeTrue();
        result.Version.Should().Be(grant.Version);
    }

    [Fact]
    public async Task ExecuteAsync_WhenGrantMissing_ThrowsNotFoundException()
    {
        // Arrange
        var grantId = BreakGlassGrantId.New();
        _grantRepo.GetByIdAsync(grantId, Arg.Any<CancellationToken>())
            .Returns((BreakGlassGrant?)null);

        // Act
        var act = () => CreateSut().ExecuteAsync(new GetBreakGlassGrantByIdQuery(grantId.Value), CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>();
    }
}
