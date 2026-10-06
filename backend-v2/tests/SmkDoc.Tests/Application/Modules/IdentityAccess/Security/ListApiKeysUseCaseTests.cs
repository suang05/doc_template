using FluentAssertions;
using Moq;
using SmkDoc.Application.Modules.IdentityAccess.Security.Queries.ListApiKeys;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Exceptions;
using SmkDoc.Domain.ValueObjects;
using Xunit;

namespace SmkDoc.Tests.Application.Modules.IdentityAccess.Security;

public class ListApiKeysUseCaseTests
{
    private readonly Mock<IApiKeyRepository> _mockRepo = new();

    [Fact]
    public async Task ExecuteAsync_WhenEmptyProjectId_ShouldThrowDomainValidationException()
    {
        var useCase = new ListApiKeysUseCase(_mockRepo.Object);
        var act = () => useCase.ExecuteAsync(new ListApiKeysQuery(Guid.Empty));

        await act.Should().ThrowAsync<DomainValidationException>()
            .WithMessage("*ProjectId cannot be empty*");
    }

    [Fact]
    public async Task ExecuteAsync_WhenValidProjectId_ShouldReturnProjectScopedApiKeys()
    {
        var projectId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var key1 = ApiKey.Issue(projectId, ApiKeyName.Create("ERP Key"), "erp", new Sha256Hash(new string('1', 64)),
            ExpirationPolicy.Never, now);
        var key2 = ApiKey.Issue(projectId, ApiKeyName.Create("CRM Key"), "crm", new Sha256Hash(new string('2', 64)),
            ExpirationPolicy.Never, now);

        _mockRepo.Setup(r => r.ListByProjectAsync(projectId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ApiKey> { key1, key2 });

        var useCase = new ListApiKeysUseCase(_mockRepo.Object);
        var result = await useCase.ExecuteAsync(new ListApiKeysQuery(projectId));

        result.Should().HaveCount(2);
        result[0].Name.Should().Be("ERP Key");
        result[1].Name.Should().Be("CRM Key");
        _mockRepo.Verify(r => r.ListByProjectAsync(projectId, It.IsAny<CancellationToken>()), Times.Once);
    }
}
