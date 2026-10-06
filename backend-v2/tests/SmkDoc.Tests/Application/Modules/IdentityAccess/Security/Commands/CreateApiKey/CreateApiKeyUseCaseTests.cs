using SmkDoc.Application.Common.Exceptions;
using SmkDoc.Application.Modules.IdentityAccess.Security.Commands.CreateApiKey;
using SmkDoc.Application.Modules.IdentityAccess.Security.DTOs;
using SmkDoc.Application.Modules.IdentityAccess.Security.Helpers;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.ValueObjects;
using SmkDoc.Tests.Common.Builders;

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
        var project = ProjectBuilder.AProject()
            .WithId(projectId)
            .WithName("Sales Project")
            .WithSlug("sales-proj")
            .Build();

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

    [Fact]
    public async Task ExecuteAsync_WhenValidationFails_ThrowsValidationExceptionAndDoesNotPersist()
    {
        // Arrange
        var command = new CreateApiKeyCommand("", "");

        // Act
        var act = () => CreateSut().ExecuteAsync(command);

        // Assert
        await act.Should().ThrowAsync<ValidationException>();
        _apiKeyRepoMock.Verify(r => r.AddAsync(It.IsAny<ApiKey>(), It.IsAny<CancellationToken>()), Times.Never);
        _uowMock.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
