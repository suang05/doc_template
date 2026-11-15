using SmkDoc.Application.Modules.IdentityAccess.Security.DTOs;
using SmkDoc.Application.Modules.IdentityAccess.Security.Queries.GetApiKeyById;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.ValueObjects.Ids;
using SmkDoc.Tests.Common.Builders;

namespace SmkDoc.Tests.Application.Modules.IdentityAccess.Security.Queries.GetApiKeyById;

public sealed class GetApiKeyByIdUseCaseTests
{
    private readonly IApiKeyRepository _apiKeyRepo = Substitute.For<IApiKeyRepository>();

    private GetApiKeyByIdUseCase CreateSut() => new(_apiKeyRepo);

    [Fact]
    public async Task ExecuteAsync_WhenProjectIdOmitted_UsesCurrentProjectLookupAndReturnsDtoWithVersion()
    {
        // Arrange
        var projectId = ProjectId.New();
        ApiKey key = new ApiKeyBuilder()
            .WithProjectId(projectId.Value)
            .WithName("Billing Key")
            .WithCallerApp("billing")
            .WithTime(TestConstants.BaselineTime);

        _apiKeyRepo.GetForCurrentProjectAsync(key.Id, Arg.Any<CancellationToken>())
            .Returns(key);

        // Act
        ApiKeyResultDto result = await CreateSut().ExecuteAsync(new GetApiKeyByIdQuery(key.Id.Value), CancellationToken.None);

        // Assert
        result.Id.Should().Be(key.Id.Value);
        result.Name.Should().Be("Billing Key");
        result.CallerApp.Should().Be("billing");
        result.Version.Should().Be(key.Version);
        await _apiKeyRepo.Received(1).GetForCurrentProjectAsync(key.Id, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenExplicitProjectIdProvided_ScopesLookupToExplicitProject()
    {
        // Arrange
        var projectId = ProjectId.New();
        var key = ApiKey.IssueStructured(
            projectId,
            ApiKeyName.Create("Scoped Key"),
            "erp",
            "smk_live",
            "1234567890ab",
            "wxyz",
            new byte[32],
            ExpirationPolicy.Never,
            TestConstants.BaselineTime);

        _apiKeyRepo.GetByIdAsync(key.Id, projectId, Arg.Any<CancellationToken>())
            .Returns(key);

        // Act
        var result = await CreateSut().ExecuteAsync(new GetApiKeyByIdQuery(key.Id.Value, projectId.Value), CancellationToken.None);

        // Assert
        result.Id.Should().Be(key.Id.Value);
        result.MaskedKey.Should().Be("smk_live_1234567890ab_****wxyz");
        await _apiKeyRepo.DidNotReceive().GetForCurrentProjectAsync(Arg.Any<ApiKeyId>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenKeyNotFoundInProject_ThrowsNotFoundException()
    {
        // Arrange
        var keyId = Guid.NewGuid();
        _apiKeyRepo.GetForCurrentProjectAsync(new ApiKeyId(keyId), Arg.Any<CancellationToken>())
            .Returns((ApiKey?)null);

        // Act
        var act = () => CreateSut().ExecuteAsync(new GetApiKeyByIdQuery(keyId), CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<NotFoundException>();
    }
}
