using SmkDoc.Application.Modules.IdentityAccess.Security.DTOs;
using SmkDoc.Application.Modules.IdentityAccess.Security.Queries.ListApiKeys;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Exceptions;
using SmkDoc.Domain.Interfaces;
using SmkDoc.Domain.ValueObjects;
using SmkDoc.Domain.ValueObjects.Ids;
using SmkDoc.Tests.Common.Builders;

namespace SmkDoc.Tests.Application.Modules.IdentityAccess.Security.Queries.ListApiKeys;

public sealed class ListApiKeysUseCaseTests
{
    private readonly IApiKeyRepository _apiKeyRepo = Substitute.For<IApiKeyRepository>();

    private ListApiKeysUseCase CreateSut() => new(_apiKeyRepo);

    [Fact]
    public async Task ExecuteAsync_WhenEmptyProjectId_ThrowsDomainValidationException()
    {
        // Act
        var act = () => CreateSut().ExecuteAsync(new ListApiKeysQuery(Guid.Empty));

        // Assert
        await act.Should().ThrowAsync<DomainValidationException>()
            .WithMessage("*ProjectId cannot be empty*");
    }

    [Fact]
    public async Task ExecuteAsync_WhenValidProjectId_ReturnsProjectScopedApiKeys()
    {
        // Arrange
        var projectId = Guid.NewGuid();
        var now = TestConstants.BaselineTime;
        ApiKey key1 = new ApiKeyBuilder()
            .WithProjectId(projectId)
            .WithName("ERP Key")
            .WithCallerApp("erp")
            .WithKeyHash(new Sha256Hash(new string('1', 64)))
            .WithExpiration(ExpirationPolicy.Never)
            .WithTime(now);
        ApiKey key2 = new ApiKeyBuilder()
            .WithProjectId(projectId)
            .WithName("CRM Key")
            .WithCallerApp("crm")
            .WithKeyHash(new Sha256Hash(new string('2', 64)))
            .WithExpiration(ExpirationPolicy.Never)
            .WithTime(now);

        _apiKeyRepo.ListByProjectAsync(new ProjectId(projectId), Arg.Any<CancellationToken>())
            .Returns(new List<ApiKey> { key1, key2 });

        // Act
        List<ApiKeyDto> result = await CreateSut().ExecuteAsync(new ListApiKeysQuery(projectId));

        // Assert
        result.Should().HaveCount(2);
        result[0].Name.Should().Be("ERP Key");
        result[1].Name.Should().Be("CRM Key");
        await _apiKeyRepo.Received(1).ListByProjectAsync(new ProjectId(projectId), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenProjectHasNoApiKeys_ReturnsEmptyList()
    {
        // Arrange
        var projectId = Guid.NewGuid();
        _apiKeyRepo.ListByProjectAsync(new ProjectId(projectId), Arg.Any<CancellationToken>())
            .Returns(new List<ApiKey>());

        // Act
        List<ApiKeyDto> result = await CreateSut().ExecuteAsync(new ListApiKeysQuery(projectId));

        // Assert
        result.Should().BeEmpty();
        await _apiKeyRepo.Received(1).ListByProjectAsync(new ProjectId(projectId), Arg.Any<CancellationToken>());
    }
}
