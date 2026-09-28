using FluentAssertions;
using Moq;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.IdentityAccess.Security;
using SmkDoc.Domain.Entities;
using Xunit;
using SmkDoc.Domain.Interfaces;

namespace SmkDoc.Tests.Application.Modules.IdentityAccess.Security;

public class ApiKeyUseCaseTests
{
    [Fact]
    public async Task CreateKeyAsync_ShouldReturnPlainTextKey_AndStoreHashedKey()
    {
        var mockRepo = new Mock<IRepository<ApiKey>>();
        var mockUow = new Mock<IUnitOfWork>();

        ApiKey? capturedKey = null;
        mockRepo.Setup(r => r.AddAsync(It.IsAny<ApiKey>(), It.IsAny<CancellationToken>()))
            .Callback<ApiKey, CancellationToken>((k, _) => capturedKey = k)
            .Returns(Task.CompletedTask);

        var useCase = new ApiKeyUseCase(mockRepo.Object, mockUow.Object);

        var result = await useCase.CreateKeyAsync("Sales App", "sales");

        result.Should().NotBeNull();
        result.PlainTextKey.Should().StartWith("smk_sales_");
        capturedKey.Should().NotBeNull();
        capturedKey!.KeyHash.Should().Be(ApiKeyUseCase.ComputeHash(result.PlainTextKey));
        mockUow.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
