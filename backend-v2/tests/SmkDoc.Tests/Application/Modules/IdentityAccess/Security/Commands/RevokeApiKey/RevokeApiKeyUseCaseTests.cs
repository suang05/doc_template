using SmkDoc.Application.Common.Exceptions;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.IdentityAccess.Security.Commands.RevokeApiKey;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Exceptions;
using SmkDoc.Domain.Interfaces;
using SmkDoc.Domain.ValueObjects;
using SmkDoc.Domain.ValueObjects.Ids;
using SmkDoc.Tests.Common.Builders;

namespace SmkDoc.Tests.Application.Modules.IdentityAccess.Security.Commands.RevokeApiKey;

public sealed class RevokeApiKeyUseCaseTests
{
    private readonly IApiKeyRepository _apiKeyRepo = Substitute.For<IApiKeyRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly ICurrentProjectContext _projectContext = Substitute.For<ICurrentProjectContext>();
    private readonly FakeTimeProvider _timeProvider = TestConstants.CreateFakeClock();

    public RevokeApiKeyUseCaseTests()
    {
        _unitOfWork.CommitAsync(Arg.Any<CancellationToken>()).Returns(1);
        _projectContext.IsResolved.Returns(true);
        _projectContext.ProjectId.Returns(new ProjectId(TestConstants.DefaultProjectId));
    }

    private RevokeApiKeyUseCase CreateSut() => new(_apiKeyRepo, _unitOfWork, _projectContext, _timeProvider);

    [Fact]
    public async Task ExecuteAsync_WhenProjectContextUnresolved_ThrowsForbiddenException()
    {
        // Arrange
        _projectContext.IsResolved.Returns(false);

        // Act
        var act = () => CreateSut().ExecuteAsync(new RevokeApiKeyCommand(Guid.NewGuid()));

        // Assert
        await act.Should().ThrowAsync<ForbiddenException>()
            .WithMessage("*Active project context is required*");
        await _unitOfWork.DidNotReceive().CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenKeyBelongsToDifferentProject_ThrowsNotFoundException()
    {
        // Arrange
        var projectA = Guid.NewGuid();
        var keyId = Guid.NewGuid();
        _projectContext.ProjectId.Returns(new ProjectId(projectA));

        _apiKeyRepo.GetByIdAsync(new ApiKeyId(keyId), new ProjectId(projectA), Arg.Any<CancellationToken>())
            .Returns((ApiKey?)null);

        // Act
        var act = () => CreateSut().ExecuteAsync(new RevokeApiKeyCommand(keyId));

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
        _projectContext.ProjectId.Returns(new ProjectId(projectId));

        ApiKey key = new ApiKeyBuilder()
            .WithProjectId(projectId)
            .WithName("Ops-Token")
            .WithCallerApp("ops-service")
            .WithKeyHash(new Sha256Hash(new string('a', 64)))
            .WithExpiration(ExpirationPolicy.Never)
            .WithTime(TestConstants.BaselineTime);

        _apiKeyRepo.GetByIdAsync(key.Id, new ProjectId(projectId), Arg.Any<CancellationToken>())
            .Returns(key);

        // Act
        await CreateSut().ExecuteAsync(new RevokeApiKeyCommand(key.Id.Value));

        // Assert
        key.IsActive.Should().BeFalse();
        key.UpdatedAt.Should().NotBeNull();
        _apiKeyRepo.Received(1).Update(key);
        await _unitOfWork.Received(1).CommitAsync(Arg.Any<CancellationToken>());
    }
}
