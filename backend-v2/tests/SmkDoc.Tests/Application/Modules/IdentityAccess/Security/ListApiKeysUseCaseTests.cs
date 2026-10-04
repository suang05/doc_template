using FluentAssertions;
using Moq;
using SmkDoc.Application.Modules.IdentityAccess.Security.Queries.ListApiKeys;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Exceptions;
using SmkDoc.Domain.Interfaces;
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
        var key1 = new ApiKey(projectId, "ERP Key", "erp", "hash1", null);
        var key2 = new ApiKey(projectId, "CRM Key", "crm", "hash2", null);

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
