using FluentAssertions;
using Moq;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.IdentityAccess.Security.Commands.RevokeApiKey;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Exceptions;
using SmkDoc.Domain.Interfaces;
using SmkDoc.Domain.ValueObjects;
using Xunit;

namespace SmkDoc.Tests.Application.Modules.IdentityAccess.Security;

public class RevokeApiKeyUseCaseTests
{
    private readonly Mock<IApiKeyRepository> _mockRepo = new();
    private readonly Mock<IUnitOfWork> _mockUow = new();

    [Fact]
    public async Task ExecuteAsync_WhenEmptyProjectId_ShouldThrowDomainValidationException()
    {
        var useCase = new RevokeApiKeyUseCase(_mockRepo.Object, _mockUow.Object);
        var act = () => useCase.ExecuteAsync(new RevokeApiKeyCommand(Guid.NewGuid(), Guid.Empty));

        await act.Should().ThrowAsync<DomainValidationException>()
            .WithMessage("*ProjectId cannot be empty*");
    }

    [Fact]
    public async Task ExecuteAsync_WhenKeyBelongsToDifferentProject_ShouldThrowNotFoundException()
    {
        var projectA = Guid.NewGuid();
        var projectB = Guid.NewGuid();
        var keyId = Guid.NewGuid();

        // Repo lookup with projectA returns null because key belongs to projectB
        _mockRepo.Setup(r => r.GetByIdAsync(keyId, projectA, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ApiKey?)null);

        var useCase = new RevokeApiKeyUseCase(_mockRepo.Object, _mockUow.Object);
        var act = () => useCase.ExecuteAsync(new RevokeApiKeyCommand(keyId, projectA));

        // IDOR Attempt blocked — 404 returned without leaking existence in Project B
        await act.Should().ThrowAsync<NotFoundException>();
        _mockRepo.Verify(r => r.Update(It.IsAny<ApiKey>()), Times.Never);
        _mockUow.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_WhenKeyExistsInProject_ShouldRevokeAndCommit()
    {
        var projectId = Guid.NewGuid();
        var key = ApiKey.Issue(projectId, ApiKeyName.Create("Test Key"), "test-caller", new Sha256Hash(new string('a', 64)), ExpirationPolicy.Never, DateTimeOffset.UtcNow);
        var keyId = key.Id;

        _mockRepo.Setup(r => r.GetByIdAsync(keyId, projectId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(key);

        var useCase = new RevokeApiKeyUseCase(_mockRepo.Object, _mockUow.Object);
        await useCase.ExecuteAsync(new RevokeApiKeyCommand(keyId, projectId));

        key.IsActive.Should().BeFalse();
        _mockRepo.Verify(r => r.Update(key), Times.Once);
        _mockUow.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
