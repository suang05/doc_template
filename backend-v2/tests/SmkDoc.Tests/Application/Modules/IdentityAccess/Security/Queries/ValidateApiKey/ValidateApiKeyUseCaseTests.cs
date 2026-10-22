using SmkDoc.Application.Modules.IdentityAccess.Security.DTOs;
using SmkDoc.Application.Modules.IdentityAccess.Security.Helpers;
using SmkDoc.Application.Modules.IdentityAccess.Security.Queries.ValidateApiKey;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Interfaces;
using SmkDoc.Domain.ValueObjects;
using SmkDoc.Tests.Common.Builders;

namespace SmkDoc.Tests.Application.Modules.IdentityAccess.Security.Queries.ValidateApiKey;

public sealed class ValidateApiKeyUseCaseTests
{
    private readonly IApiKeyRepository _apiKeyRepo = Substitute.For<IApiKeyRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly FakeTimeProvider _clock = TestConstants.CreateFakeClock();

    public ValidateApiKeyUseCaseTests()
    {
        _unitOfWork.CommitAsync(Arg.Any<CancellationToken>()).Returns(1);
    }

    private ValidateApiKeyUseCase CreateSut() =>
        new(_apiKeyRepo, _unitOfWork, _clock);

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
        await _apiKeyRepo.DidNotReceive().GetByKeyHashAsync(Arg.Any<Sha256Hash>(), Arg.Any<CancellationToken>());
        await _unitOfWork.DidNotReceive().CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenKeyNotFoundInRepo_ReturnsNull()
    {
        // Arrange
        _apiKeyRepo.GetByKeyHashAsync(Arg.Any<Sha256Hash>(), Arg.Any<CancellationToken>())
            .Returns((ApiKey?)null);

        // Act
        var result = await CreateSut().ExecuteAsync(new ValidateApiKeyQuery("smk_test_key_not_found"));

        // Assert
        result.Should().BeNull();
        _apiKeyRepo.DidNotReceive().Update(Arg.Any<ApiKey>());
        await _unitOfWork.DidNotReceive().CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenKeyIsExpiredOrInactive_ReturnsNullAndDoesNotRecordUsage()
    {
        // Arrange
        ApiKey key = new ApiKeyBuilder().WithTime(TestConstants.BaselineTime);
        key.Revoke(TestConstants.BaselineTime);

        _apiKeyRepo.GetByKeyHashAsync(Arg.Any<Sha256Hash>(), Arg.Any<CancellationToken>())
            .Returns(key);

        // Act
        var result = await CreateSut().ExecuteAsync(new ValidateApiKeyQuery("smk_test_inactive_key"));

        // Assert
        result.Should().BeNull();
        _apiKeyRepo.DidNotReceive().Update(Arg.Any<ApiKey>());
        await _unitOfWork.DidNotReceive().CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenKeyIsValid_RecordsUsageUpdatesRepoAndReturnsDto()
    {
        // Arrange
        const string plainText = "smk_billing_secret123";
        var hash = new Sha256Hash(ApiKeyHelper.ComputeHash(plainText));
        var projectId = Guid.NewGuid();
        ApiKey key = new ApiKeyBuilder()
            .WithProjectId(projectId)
            .WithCallerApp("billing-service")
            .WithKeyHash(hash)
            .WithTime(TestConstants.BaselineTime);

        _apiKeyRepo.GetByKeyHashAsync(hash, Arg.Any<CancellationToken>())
            .Returns(key);

        // Act
        ValidatedApiKeyDto? result = await CreateSut().ExecuteAsync(new ValidateApiKeyQuery(plainText));

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(key.Id);
        result.CallerApp.Should().Be("billing-service");
        result.ProjectId.Should().Be(projectId);

        key.LastUsedAt.Should().Be(TestConstants.BaselineTime);
        _apiKeyRepo.Received(1).Update(key);
        await _unitOfWork.Received(1).CommitAsync(Arg.Any<CancellationToken>());
    }
}
