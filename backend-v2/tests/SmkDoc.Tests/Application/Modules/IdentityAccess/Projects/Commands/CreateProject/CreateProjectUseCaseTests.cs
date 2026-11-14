using SmkDoc.Application.Common.Behaviors;
using SmkDoc.Application.Modules.IdentityAccess.Projects.Commands.CreateProject;
using SmkDoc.Application.Modules.IdentityAccess.Projects.DTOs;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.ValueObjects.Ids;
using SmkDoc.Tests.Common.Builders;
using ValidationException = SmkDoc.Application.Common.Exceptions.ValidationException;

namespace SmkDoc.Tests.Application.Modules.IdentityAccess.Projects.Commands.CreateProject;

public sealed class CreateProjectUseCaseTests
{
    private readonly IProjectRepository _projectRepo = Substitute.For<IProjectRepository>();
    private readonly IUserProjectRoleRepository _roleRepo = Substitute.For<IUserProjectRoleRepository>();
    private readonly IApiKeyRepository _apiKeyRepo = Substitute.For<IApiKeyRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly FakeTimeProvider _timeProvider = TestConstants.CreateFakeClock();

    public CreateProjectUseCaseTests()
    {
        _unitOfWork.CommitAsync(Arg.Any<CancellationToken>()).Returns(1);
    }

    private CreateProjectUseCase CreateSut() =>
        new(
            _projectRepo,
            _roleRepo,
            _apiKeyRepo,
            _unitOfWork,
            _timeProvider);

    [Fact]
    public async Task ExecuteAsync_WhenSlugAlreadyExists_ThrowsConflictException()
    {
        // Arrange
        var now = TestConstants.BaselineTime;
        var command = new CreateProjectCommand(Guid.NewGuid(), "Existing Project", "existing-slug");
        Project existingProject = ProjectBuilder.AProject()
            .WithName("Existing Project")
            .WithSlug("existing-slug")
            .WithTime(now);

        _projectRepo.GetBySlugAsync("existing-slug", Arg.Any<CancellationToken>())
            .Returns(existingProject);

        // Act
        var act = () => CreateSut().ExecuteAsync(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ConflictException>();
        await _projectRepo.DidNotReceive().AddAsync(Arg.Any<Project>(), Arg.Any<CancellationToken>());
        await _unitOfWork.DidNotReceive().CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenValidCommand_CreatesProjectAssignsAdminAndProvisionsApiKeys()
    {
        // Arrange
        var now = TestConstants.BaselineTime;
        var userId = Guid.NewGuid();
        var companyId = CompanyId.New();
        var command = new CreateProjectCommand(userId, "New Project", "new-project");

        _projectRepo.GetBySlugAsync("new-project", Arg.Any<CancellationToken>())
            .Returns((Project?)null);
        _projectRepo.GetOrCreateDefaultCompanyIdAsync(now, Arg.Any<CancellationToken>())
            .Returns(companyId);

        Project? capturedProject = null;
        _projectRepo.AddAsync(Arg.Do<Project>(p => capturedProject = p), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        UserProjectRole? capturedRole = null;
        _roleRepo.AddAsync(Arg.Do<UserProjectRole>(r => capturedRole = r), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        // Act
        ProjectResultDto result = await CreateSut().ExecuteAsync(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Name.Should().Be("New Project");
        result.Slug.Should().Be("new-project");
        result.ReadApiKey.Should().NotBeNullOrWhiteSpace();
        result.WriteApiKey.Should().NotBeNullOrWhiteSpace();

        capturedProject.Should().NotBeNull();
        capturedRole.Should().NotBeNull();
        capturedRole!.UserId.Value.Should().Be(userId);
        capturedRole.Role.Should().Be(RoleType.Admin);

        await _apiKeyRepo.Received(2).AddAsync(Arg.Any<ApiKey>(), Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenValidationFails_ThrowsValidationExceptionAndDoesNotPersist()
    {
        // Arrange
        var command = new CreateProjectCommand(Guid.Empty, "", "");
        var pipeline = new ValidatingUseCaseDecorator<CreateProjectCommand, ProjectResultDto>(
            CreateSut(),
            [new CreateProjectCommandValidator()]);

        // Act
        var act = () => pipeline.ExecuteAsync(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ValidationException>();
        await _projectRepo.DidNotReceive().AddAsync(Arg.Any<Project>(), Arg.Any<CancellationToken>());
        await _unitOfWork.DidNotReceive().CommitAsync(Arg.Any<CancellationToken>());
    }
}
