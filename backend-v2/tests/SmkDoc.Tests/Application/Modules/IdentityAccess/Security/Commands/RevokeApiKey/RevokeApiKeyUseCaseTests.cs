using SmkDoc.Application.Modules.IdentityAccess.Security.Commands.RevokeApiKey;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Exceptions;

namespace SmkDoc.Tests.Application.Modules.IdentityAccess.Security.Commands.RevokeApiKey;

public class RevokeApiKeyUseCaseTests
{
    private readonly Mock<IApiKeyRepository> _mockRepo = new();
    private readonly Mock<IUnitOfWork> _mockUow = new();

    private RevokeApiKeyUseCase CreateSut() => new(_mockRepo.Object, _mockUow.Object);

    [Fact]
    public async Task ExecuteAsync_WhenEmptyProjectId_ThrowsDomainValidationException()
    {
        // Act
        var act = () => CreateSut().ExecuteAsync(new RevokeApiKeyCommand(Guid.NewGuid(), Guid.Empty));

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

        _mockRepo.Setup(r => r.GetByIdAsync(keyId, projectA, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ApiKey?)null);

        // Act
        var act = () => CreateSut().ExecuteAsync(new RevokeApiKeyCommand(keyId, projectA));

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
            ApiKeyName.Create("Ops-Token"),
            "ops-service",
            new Sha256Hash(new string('a', 64)),
            ExpirationPolicy.Never,
            TestConstants.BaselineTime);

        _mockRepo.Setup(r => r.GetByIdAsync(key.Id, projectId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(key);

        // Act
        await CreateSut().ExecuteAsync(new RevokeApiKeyCommand(key.Id, projectId));

        // Assert
        key.IsActive.Should().BeFalse();
        key.UpdatedAt.Should().NotBeNull();
        _mockRepo.Verify(r => r.Update(key), Times.Once);
        _mockUow.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
