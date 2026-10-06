using Microsoft.Extensions.Time.Testing;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.IdentityAccess.Security.DTOs;
using SmkDoc.Application.Modules.IdentityAccess.Security.Helpers;
using SmkDoc.Application.Modules.IdentityAccess.Security.Queries.ValidateApiKey;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Interfaces;
using SmkDoc.Domain.ValueObjects;
using SmkDoc.Tests.Common;
using SmkDoc.Tests.Common.Factories;

namespace SmkDoc.Tests.Application.Modules.IdentityAccess.Security.Queries.ValidateApiKey;

public class ValidateApiKeyUseCaseTests
{
    private readonly Mock<IApiKeyRepository> _apiKeyRepoMock = new();
    private readonly Mock<IUnitOfWork> _uowMock = new();
    private readonly FakeTimeProvider _clock = TestConstants.CreateFakeClock();

    private ValidateApiKeyUseCase CreateSut() =>
        new(_apiKeyRepoMock.Object, _uowMock.Object, _clock);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ExecuteAsync_WhenPlainTextKeyIsNullOrWhitespace_ReturnsNullWithoutQueryingRepo(string? invalidKey)
    {
        // Act
        var result = await CreateSut().ExecuteAsync(new ValidateApiKeyQuery(invalidKey!));

        // Assert
        result.Should().BeNull();
        _apiKeyRepoMock.Verify(r => r.GetByKeyHashAsync(It.IsAny<Sha256Hash>(), It.IsAny<CancellationToken>()), Times.Never);
        _uowMock.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_WhenKeyNotFoundInRepo_ReturnsNull()
    {
        // Arrange
        _apiKeyRepoMock.Setup(r => r.GetByKeyHashAsync(It.IsAny<Sha256Hash>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ApiKey?)null);

        // Act
        var result = await CreateSut().ExecuteAsync(new ValidateApiKeyQuery("smk_test_key_not_found"));

        // Assert
        result.Should().BeNull();
        _apiKeyRepoMock.Verify(r => r.Update(It.IsAny<ApiKey>()), Times.Never);
        _uowMock.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_WhenKeyIsExpiredOrInactive_ReturnsNullAndDoesNotRecordUsage()
    {
        // Arrange
        var key = ApiKeyTestFactory.Create(now: TestConstants.BaselineTime);
        key.Revoke(TestConstants.BaselineTime);

        _apiKeyRepoMock.Setup(r => r.GetByKeyHashAsync(It.IsAny<Sha256Hash>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(key);

        // Act
        var result = await CreateSut().ExecuteAsync(new ValidateApiKeyQuery("smk_test_inactive_key"));

        // Assert
        result.Should().BeNull();
        _apiKeyRepoMock.Verify(r => r.Update(It.IsAny<ApiKey>()), Times.Never);
        _uowMock.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_WhenKeyIsValid_RecordsUsageUpdatesRepoAndReturnsDto()
    {
        // Arrange
        const string plainText = "smk_billing_secret123";
        var hash = new Sha256Hash(ApiKeyHelper.ComputeHash(plainText));
        var projectId = Guid.NewGuid();
        var key = ApiKeyTestFactory.Create(
            projectId: projectId,
            callerApp: "billing-service",
            keyHash: hash,
            now: TestConstants.BaselineTime);

        _apiKeyRepoMock.Setup(r => r.GetByKeyHashAsync(hash, It.IsAny<CancellationToken>()))
            .ReturnsAsync(key);

        // Act
        var result = await CreateSut().ExecuteAsync(new ValidateApiKeyQuery(plainText));

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(key.Id);
        result.CallerApp.Should().Be("billing-service");
        result.ProjectId.Should().Be(projectId);

        key.LastUsedAt.Should().Be(TestConstants.BaselineTime);
        _apiKeyRepoMock.Verify(r => r.Update(key), Times.Once);
        _uowMock.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
