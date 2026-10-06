using SmkDoc.Application.Modules.IdentityAccess.Security.Commands.CreateApiKey;
using SmkDoc.Application.Modules.IdentityAccess.Security.DTOs;
using SmkDoc.Application.Modules.IdentityAccess.Security.Helpers;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.ValueObjects;

namespace SmkDoc.Tests.Application.Modules.IdentityAccess.Security.Commands.CreateApiKey;

public class CreateApiKeyUseCaseTests
{
    private readonly Mock<IApiKeyRepository> _apiKeyRepoMock = new();
    private readonly Mock<IProjectRepository> _projectRepoMock = new();
    private readonly Mock<IUnitOfWork> _uowMock = new();
    private readonly CreateApiKeyCommandValidator _validator = new();

    private CreateApiKeyUseCase CreateSut() =>
        new(_apiKeyRepoMock.Object, _projectRepoMock.Object, _uowMock.Object, _validator);

    [Fact]
    public async Task ExecuteAsync_WhenValidCommand_ReturnsPlainTextKeyAndPersistsHashedKey()
    {
        // Arrange
        ApiKey? capturedKey = null;
        _apiKeyRepoMock.Setup(r => r.AddAsync(It.IsAny<ApiKey>(), It.IsAny<CancellationToken>()))
            .Callback<ApiKey, CancellationToken>((k, _) => capturedKey = k)
            .Returns(Task.CompletedTask);

        var projectId = Guid.NewGuid();
        var project = Project.Create(
            Guid.NewGuid(),
            ProjectName.Create("Sales Project"),
            TemplateSlug.Create("sales-proj"),
            TestConstants.BaselineTime);

        _projectRepoMock.Setup(r => r.GetByIdAsync(projectId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(project);

        var command = new CreateApiKeyCommand("Sales App", "sales", projectId);

        // Act
        var result = await CreateSut().ExecuteAsync(command);

        // Assert
        result.Should().NotBeNull();
        result.PlainTextKey.Should().StartWith("smk_sales_");

        capturedKey.Should().NotBeNull();
        capturedKey!.KeyHash.Value.Should().Be(ApiKeyHelper.ComputeHash(result.PlainTextKey));

        _uowMock.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
