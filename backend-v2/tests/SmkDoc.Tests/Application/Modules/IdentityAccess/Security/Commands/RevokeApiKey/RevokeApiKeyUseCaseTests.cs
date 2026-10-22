using SmkDoc.Application.Modules.IdentityAccess.Security.Commands.RevokeApiKey;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Exceptions;
using SmkDoc.Domain.Interfaces;
using SmkDoc.Domain.ValueObjects;
using SmkDoc.Tests.Common.Builders;

namespace SmkDoc.Tests.Application.Modules.IdentityAccess.Security.Commands.RevokeApiKey;

public sealed class RevokeApiKeyUseCaseTests
{
    private readonly IApiKeyRepository _apiKeyRepo = Substitute.For<IApiKeyRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();

    public RevokeApiKeyUseCaseTests()
    {
        _unitOfWork.CommitAsync(Arg.Any<CancellationToken>()).Returns(1);
    }

    private RevokeApiKeyUseCase CreateSut() => new(_apiKeyRepo, _unitOfWork);

    [Fact]
    public async Task ExecuteAsync_WhenEmptyProjectId_ThrowsDomainValidationException()
    {
        // Act
        var act = () => CreateSut().ExecuteAsync(new RevokeApiKeyCommand(Guid.NewGuid(), Guid.Empty));

        // Assert
        await act.Should().ThrowAsync<DomainValidationException>()
            .WithMessage("*ProjectId cannot be empty*");
        await _unitOfWork.DidNotReceive().CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenKeyBelongsToDifferentProject_ThrowsNotFoundException()
    {
        // Arrange
        var projectA = Guid.NewGuid();
        var keyId = Guid.NewGuid();

        _apiKeyRepo.GetByIdAsync(keyId, projectA, Arg.Any<CancellationToken>())
            .Returns((ApiKey?)null);

        // Act
        var act = () => CreateSut().ExecuteAsync(new RevokeApiKeyCommand(keyId, projectA));

        // Assert - IDOR Attempt blocked — 404 returned without leaking existence
        await act.Should().ThrowAsync<NotFoundException>();
        _apiKeyRepo.DidNotReceive().Update(Arg.Any<ApiKey>());
        await _unitOfWork.DidNotReceive().CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenKeyExistsInProject_RevokesAndCommitsTransaction()
    {
        // Arrange
        var projectId = Guid.NewGuid();
        ApiKey key = new ApiKeyBuilder()
            .WithProjectId(projectId)
            .WithName("Ops-Token")
            .WithCallerApp("ops-service")
            .WithKeyHash(new Sha256Hash(new string('a', 64)))
            .WithExpiration(ExpirationPolicy.Never)
            .WithTime(TestConstants.BaselineTime);

        _apiKeyRepo.GetByIdAsync(key.Id, projectId, Arg.Any<CancellationToken>())
            .Returns(key);

        // Act
        await CreateSut().ExecuteAsync(new RevokeApiKeyCommand(key.Id, projectId));

        // Assert
        key.IsActive.Should().BeFalse();
        key.UpdatedAt.Should().NotBeNull();
        _apiKeyRepo.Received(1).Update(key);
        await _unitOfWork.Received(1).CommitAsync(Arg.Any<CancellationToken>());
    }
}
