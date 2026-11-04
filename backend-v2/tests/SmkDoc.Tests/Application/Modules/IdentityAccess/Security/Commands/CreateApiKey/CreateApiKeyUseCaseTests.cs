using SmkDoc.Application.Common.Behaviors;
using SmkDoc.Application.Common.Exceptions;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.IdentityAccess.Security.Commands.CreateApiKey;
using SmkDoc.Application.Modules.IdentityAccess.Security.DTOs;
using SmkDoc.Application.Modules.IdentityAccess.Security.Helpers;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Enums;
using SmkDoc.Domain.Interfaces;
using SmkDoc.Domain.ValueObjects.Ids;
using SmkDoc.Tests.Common.Builders;

namespace SmkDoc.Tests.Application.Modules.IdentityAccess.Security.Commands.CreateApiKey;

public sealed class CreateApiKeyUseCaseTests
{
    private readonly IApiKeyRepository _apiKeyRepo = Substitute.For<IApiKeyRepository>();
    private readonly IProjectRepository _projectRepo = Substitute.For<IProjectRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly ICurrentProjectContext _projectContext = Substitute.For<ICurrentProjectContext>();
    private readonly FakeTimeProvider _timeProvider = TestConstants.CreateFakeClock();

    public CreateApiKeyUseCaseTests()
    {
        _unitOfWork.CommitAsync(Arg.Any<CancellationToken>()).Returns(1);
        _projectContext.IsResolved.Returns(true);
        _projectContext.ProjectId.Returns(new ProjectId(TestConstants.DefaultProjectId));
    }

    private CreateApiKeyUseCase CreateSut() =>
        new(_apiKeyRepo, _projectRepo, _unitOfWork, _projectContext, _timeProvider);

    [Fact]
    public async Task ExecuteAsync_WhenValidCommand_ReturnsPlainTextKeyAndPersistsHashedKeyForAmbientProject()
    {
        // Arrange
        ApiKey? capturedKey = null;
        _apiKeyRepo.AddAsync(Arg.Do<ApiKey>(k => capturedKey = k), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var projectId = Guid.NewGuid();
        _projectContext.ProjectId.Returns(new ProjectId(projectId));

        Project project = ProjectBuilder.AProject()
            .WithId(projectId)
            .WithName("Sales Project")
            .WithSlug("sales-proj");

        _projectRepo.GetByIdAsync(new ProjectId(projectId), Arg.Any<CancellationToken>())
            .Returns(project);
        _apiKeyRepo.ListByProjectAsync(new ProjectId(projectId), Arg.Any<CancellationToken>())
            .Returns(new List<ApiKey>());

        var command = new CreateApiKeyCommand("Sales App", "sales");

        // Act
        CreateApiKeyResultDto result = await CreateSut().ExecuteAsync(command);

        // Assert
        result.Should().NotBeNull();
        result.PlainTextKey.Should().StartWith("smk_sales_");

        capturedKey.Should().NotBeNull();
        capturedKey!.ProjectId.Should().Be(new ProjectId(projectId));
        capturedKey.KeyHash.Value.Should().Be(ApiKeyHelper.ComputeHash(result.PlainTextKey));

        await _unitOfWork.Received(1).CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenQuotaReached2ActiveKeys_ThrowsConflictException()
    {
        // Arrange
        var projectId = Guid.NewGuid();
        _projectContext.ProjectId.Returns(new ProjectId(projectId));

        Project project = ProjectBuilder.AProject().WithId(projectId);
        _projectRepo.GetByIdAsync(new ProjectId(projectId), Arg.Any<CancellationToken>())
            .Returns(project);

        var key1 = ApiKeyBuilder.AKey().WithProjectId(projectId).WithScope(ApiKeyScope.ReadOnly).Build();
        var key2 = ApiKeyBuilder.AKey().WithProjectId(projectId).WithScope(ApiKeyScope.ReadWrite).Build();
        _apiKeyRepo.ListByProjectAsync(new ProjectId(projectId), Arg.Any<CancellationToken>())
            .Returns(new List<ApiKey> { key1, key2 });

        var command = new CreateApiKeyCommand("Third Key", "sales", "ReadOnly");

        // Act
        var act = () => CreateSut().ExecuteAsync(command);

        // Assert
        await act.Should().ThrowAsync<ConflictException>()
            .WithMessage("*maximum quota of 2 active API keys*");
    }

    [Fact]
    public async Task ExecuteAsync_WhenDuplicateScopeAlreadyActive_ThrowsConflictException()
    {
        // Arrange
        var projectId = Guid.NewGuid();
        _projectContext.ProjectId.Returns(new ProjectId(projectId));

        Project project = ProjectBuilder.AProject().WithId(projectId);
        _projectRepo.GetByIdAsync(new ProjectId(projectId), Arg.Any<CancellationToken>())
            .Returns(project);

        var existingKey = ApiKeyBuilder.AKey().WithProjectId(projectId).WithScope(ApiKeyScope.ReadWrite).Build();
        _apiKeyRepo.ListByProjectAsync(new ProjectId(projectId), Arg.Any<CancellationToken>())
            .Returns(new List<ApiKey> { existingKey });

        var command = new CreateApiKeyCommand("Another Write Key", "sales", "ReadWrite");

        // Act
        var act = () => CreateSut().ExecuteAsync(command);

        // Assert
        await act.Should().ThrowAsync<ConflictException>()
            .WithMessage("*already possesses an active ReadWrite API key*");
    }

    [Fact]
    public async Task ExecuteAsync_WhenValidationFails_ThrowsValidationExceptionAndDoesNotPersist()
    {
        // Arrange
        var command = new CreateApiKeyCommand("", "");
        var pipeline = new ValidatingUseCaseDecorator<CreateApiKeyCommand, CreateApiKeyResultDto>(
            CreateSut(),
            [new CreateApiKeyCommandValidator()]);

        // Act
        var act = () => pipeline.ExecuteAsync(command);

        // Assert
        await act.Should().ThrowAsync<ValidationException>();
        await _apiKeyRepo.DidNotReceive().AddAsync(Arg.Any<ApiKey>(), Arg.Any<CancellationToken>());
        await _unitOfWork.DidNotReceive().CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenProjectContextUnresolved_ThrowsForbiddenException()
    {
        // Arrange
        _projectContext.IsResolved.Returns(false);
        var command = new CreateApiKeyCommand("Sales App", "sales");

        // Act
        var act = () => CreateSut().ExecuteAsync(command);

        // Assert
        await act.Should().ThrowAsync<ForbiddenException>()
            .WithMessage("*Active project context is required*");
        await _apiKeyRepo.DidNotReceive().AddAsync(Arg.Any<ApiKey>(), Arg.Any<CancellationToken>());
    }
}
