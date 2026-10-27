using SmkDoc.Application.Common.Exceptions;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.IdentityAccess.Security.Commands.CreateApiKey;
using SmkDoc.Application.Modules.IdentityAccess.Security.DTOs;
using SmkDoc.Application.Modules.IdentityAccess.Security.Helpers;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Interfaces;
using SmkDoc.Domain.ValueObjects;
using SmkDoc.Domain.ValueObjects.Ids;
using SmkDoc.Tests.Common.Builders;

namespace SmkDoc.Tests.Application.Modules.IdentityAccess.Security.Commands.CreateApiKey;

public sealed class CreateApiKeyUseCaseTests
{
    private readonly IApiKeyRepository _apiKeyRepo = Substitute.For<IApiKeyRepository>();
    private readonly IProjectRepository _projectRepo = Substitute.For<IProjectRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly CreateApiKeyCommandValidator _validator = new();

    public CreateApiKeyUseCaseTests()
    {
        _unitOfWork.CommitAsync(Arg.Any<CancellationToken>()).Returns(1);
    }

    private CreateApiKeyUseCase CreateSut() =>
        new(_apiKeyRepo, _projectRepo, _unitOfWork, _validator);

    [Fact]
    public async Task ExecuteAsync_WhenValidCommand_ReturnsPlainTextKeyAndPersistsHashedKey()
    {
        // Arrange
        ApiKey? capturedKey = null;
        _apiKeyRepo.AddAsync(Arg.Do<ApiKey>(k => capturedKey = k), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var projectId = Guid.NewGuid();
        Project project = ProjectBuilder.AProject()
            .WithId(projectId)
            .WithName("Sales Project")
            .WithSlug("sales-proj");

        _projectRepo.GetByIdAsync(new ProjectId(projectId), Arg.Any<CancellationToken>())
            .Returns(project);

        var command = new CreateApiKeyCommand("Sales App", "sales", projectId);

        // Act
        CreateApiKeyResultDto result = await CreateSut().ExecuteAsync(command);

        // Assert
        result.Should().NotBeNull();
        result.PlainTextKey.Should().StartWith("smk_sales_");

        capturedKey.Should().NotBeNull();
        capturedKey!.KeyHash.Value.Should().Be(ApiKeyHelper.ComputeHash(result.PlainTextKey));

        await _unitOfWork.Received(1).CommitAsync(Arg.Any<CancellationToken>());
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
        await _apiKeyRepo.DidNotReceive().AddAsync(Arg.Any<ApiKey>(), Arg.Any<CancellationToken>());
        await _unitOfWork.DidNotReceive().CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenProjectIdOmitted_ResolvesFromCurrentProjectContext()
    {
        // Arrange
        var contextProjectId = Guid.NewGuid();
        var projectContext = Substitute.For<ICurrentProjectContext>();
        projectContext.IsResolved.Returns(true);
        projectContext.ProjectId.Returns(new ProjectId(contextProjectId));

        Project project = ProjectBuilder.AProject()
            .WithId(contextProjectId)
            .WithName("Context Project")
            .WithSlug("ctx-proj");

        _projectRepo.GetByIdAsync(new ProjectId(contextProjectId), Arg.Any<CancellationToken>())
            .Returns(project);

        var sut = new CreateApiKeyUseCase(
            _apiKeyRepo,
            _projectRepo,
            _unitOfWork,
            _validator,
            projectContext: projectContext);

        var command = new CreateApiKeyCommand("Context App", "ctx-app", ProjectId: null);

        // Act
        var result = await sut.ExecuteAsync(command);

        // Assert
        result.Should().NotBeNull();
        await _apiKeyRepo.Received(1).AddAsync(
            Arg.Is<ApiKey>(k => k.ProjectId == new ProjectId(contextProjectId)),
            Arg.Any<CancellationToken>());
    }
}
