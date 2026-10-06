using SmkDoc.Application.Modules.IdentityAccess.Security.Commands.RevokeApiKey;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.ValueObjects;

namespace SmkDoc.Tests.Application.Modules.IdentityAccess.Security;

public class RevokeApiKeyUseCaseTests
{
    private readonly Mock<IApiKeyRepository> _mockRepo = new();
    private readonly Mock<IUnitOfWork> _mockUow = new();

    [Fact]
    public async Task ExecuteAsync_WhenEmptyProjectId_ThrowsDomainValidationException()
    {
        // Arrange
        var useCase = new RevokeApiKeyUseCase(_mockRepo.Object, _mockUow.Object);

        // Act
        var act = () => useCase.ExecuteAsync(new RevokeApiKeyCommand(Guid.NewGuid(), Guid.Empty));

        // Assert
        await act.Should().ThrowAsync<DomainValidationException>()
            .WithMessage("*ProjectId cannot be empty*");
    }

    [Fact]
    public async Task ExecuteAsync_WhenKeyBelongsToDifferentProject_ThrowsNotFoundException()
    {
        // Arrange
        var projectA = Guid.NewGuid();
        var keyId = Guid.NewGuid();

        // Repo lookup with projectA returns null because key belongs to another project
        _mockRepo.Setup(r => r.GetByIdAsync(keyId, projectA, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ApiKey?)null);

        var useCase = new RevokeApiKeyUseCase(_mockRepo.Object, _mockUow.Object);

        // Act
        var act = () => useCase.ExecuteAsync(new RevokeApiKeyCommand(keyId, projectA));

        // Assert - IDOR Attempt blocked — 404 returned without leaking existence
        await act.Should().ThrowAsync<NotFoundException>();
        _mockRepo.Verify(r => r.Update(It.IsAny<ApiKey>()), Times.Never);
        _mockUow.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_WhenKeyExistsInProject_RevokesAndCommitsTransaction()
    {
        // Arrange
        var projectId = Guid.NewGuid();
        var key = ApiKey.Issue(
            projectId,
            ApiKeyName.Create("Test Key"),
            "test-caller",
            new Sha256Hash(new string('a', 64)),
            ExpirationPolicy.Never,
            TestConstants.BaselineTime);
        var keyId = key.Id;

        _mockRepo.Setup(r => r.GetByIdAsync(keyId, projectId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(key);

        var useCase = new RevokeApiKeyUseCase(_mockRepo.Object, _mockUow.Object);

        // Act
        await useCase.ExecuteAsync(new RevokeApiKeyCommand(keyId, projectId));

        // Assert
        key.IsActive.Should().BeFalse();
        _mockRepo.Verify(r => r.Update(key), Times.Once);
        _mockUow.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
