using FluentAssertions;
using Moq;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.IdentityAccess.Security.Commands.CreateApiKey;
using SmkDoc.Application.Modules.IdentityAccess.Security.DTOs;
using SmkDoc.Application.Modules.IdentityAccess.Security.Helpers;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Interfaces;
using SmkDoc.Domain.ValueObjects;
using Xunit;

namespace SmkDoc.Tests.Application.Modules.IdentityAccess.Security;

public class ApiKeyUseCaseTests
{
    [Fact]
    public async Task CreateKeyAsync_ShouldReturnPlainTextKey_AndStoreHashedKey()
    {
        var mockRepo = new Mock<IApiKeyRepository>();
        var mockProjectRepo = new Mock<IProjectRepository>();
        var mockUow = new Mock<IUnitOfWork>();
        var validator = new CreateApiKeyCommandValidator();

        ApiKey? capturedKey = null;
        mockRepo.Setup(r => r.AddAsync(It.IsAny<ApiKey>(), It.IsAny<CancellationToken>()))
            .Callback<ApiKey, CancellationToken>((k, _) => capturedKey = k)
            .Returns(Task.CompletedTask);

        var projectId = Guid.NewGuid();
        mockProjectRepo.Setup(r => r.GetByIdAsync(projectId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Project.Create(Guid.NewGuid(), ProjectName.Create("Sales Project"), TemplateSlug.Create("sales-proj"), DateTimeOffset.UtcNow));

        var useCase = new CreateApiKeyUseCase(mockRepo.Object, mockProjectRepo.Object, mockUow.Object, validator);

        var result = await useCase.ExecuteAsync(new CreateApiKeyCommand("Sales App", "sales", projectId));

        result.Should().NotBeNull();
        result.PlainTextKey.Should().StartWith("smk_sales_");
        capturedKey.Should().NotBeNull();
        capturedKey!.KeyHash.Value.Should().Be(ApiKeyHelper.ComputeHash(result.PlainTextKey));
        mockUow.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
