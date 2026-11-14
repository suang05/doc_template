using SmkDoc.Application.Modules.IdentityAccess.Security.DTOs;
using SmkDoc.Application.Modules.IdentityAccess.Security.Queries.ListApiKeys;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.ValueObjects.Ids;
using SmkDoc.Tests.Common.Builders;

namespace SmkDoc.Tests.Application.Modules.IdentityAccess.Security.Queries.ListApiKeys;

public sealed class ListApiKeysUseCaseTests
{
    private readonly IApiKeyRepository _apiKeyRepo = Substitute.For<IApiKeyRepository>();

    private ListApiKeysUseCase CreateSut() => new(_apiKeyRepo);

    [Fact]
    public async Task ExecuteAsync_WhenTenantContextUnresolved_PropagatesTenantContextNotResolvedException()
    {
        // Arrange
        _apiKeyRepo.ListCurrentProjectAsync(Arg.Any<CancellationToken>())
            .Returns<IReadOnlyList<ApiKey>>(_ => throw new TenantContextNotResolvedException());

        // Act
        var act = () => CreateSut().ExecuteAsync(new ListApiKeysQuery(), CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<TenantContextNotResolvedException>();
    }

    [Fact]
    public async Task ExecuteAsync_WhenQueryProjectIdIsNull_UsesListCurrentProjectAsyncWithMaskedFormat()
    {
        // Arrange
        var projectId = Guid.NewGuid();
        var now = TestConstants.BaselineTime;
        ApiKey key1 = new ApiKeyBuilder()
            .WithProjectId(projectId)
            .WithName("ERP Key")
            .WithCallerApp("erp")
            .WithKeyHash(new Sha256Hash(new string('1', 60) + "abcd"))
            .WithExpiration(ExpirationPolicy.Never)
            .WithTime(now);
        ApiKey key2 = new ApiKeyBuilder()
            .WithProjectId(projectId)
            .WithName("CRM Key")
            .WithCallerApp("crm")
            .WithKeyHash(new Sha256Hash(new string('2', 60) + "cdef"))
            .WithExpiration(ExpirationPolicy.Never)
            .WithTime(now);

        _apiKeyRepo.ListCurrentProjectAsync(Arg.Any<CancellationToken>())
            .Returns(new List<ApiKey> { key1, key2 });

        // Act
        List<ApiKeyResultDto> result = await CreateSut().ExecuteAsync(new ListApiKeysQuery(), CancellationToken.None);

        // Assert
        result.Should().HaveCount(2);
        result[0].Name.Should().Be("ERP Key");
        result[0].MaskedKey.Should().Be("smk_erp_****abcd");
        result[1].Name.Should().Be("CRM Key");
        result[1].MaskedKey.Should().Be("smk_crm_****cdef");
        await _apiKeyRepo.Received(1).ListCurrentProjectAsync(Arg.Any<CancellationToken>());
        await _apiKeyRepo.DidNotReceive().ListAllAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenQueryProjectIdSpecified_QueriesSpecificProjectWithoutListAllBypass()
    {
        // Arrange
        var projectId = Guid.NewGuid();
        var key = new ApiKeyBuilder().WithProjectId(projectId).WithName("Scoped Key").WithCallerApp("erp").Build();
        _apiKeyRepo.ListByProjectAsync(new ProjectId(projectId), Arg.Any<CancellationToken>())
            .Returns(new List<ApiKey> { key });

        // Act
        List<ApiKeyResultDto> result = await CreateSut().ExecuteAsync(new ListApiKeysQuery(projectId), CancellationToken.None);

        // Assert
        result.Should().ContainSingle();
        result[0].Name.Should().Be("Scoped Key");
        await _apiKeyRepo.Received(1).ListByProjectAsync(new ProjectId(projectId), Arg.Any<CancellationToken>());
        await _apiKeyRepo.DidNotReceive().ListAllAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenProjectHasNoApiKeys_ReturnsEmptyList()
    {
        // Arrange
        _apiKeyRepo.ListCurrentProjectAsync(Arg.Any<CancellationToken>())
            .Returns(new List<ApiKey>());

        // Act
        List<ApiKeyResultDto> result = await CreateSut().ExecuteAsync(new ListApiKeysQuery(), CancellationToken.None);

        // Assert
        result.Should().BeEmpty();
        await _apiKeyRepo.Received(1).ListCurrentProjectAsync(Arg.Any<CancellationToken>());
    }
}
