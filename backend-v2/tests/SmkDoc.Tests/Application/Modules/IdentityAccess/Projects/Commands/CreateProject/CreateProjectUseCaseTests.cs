using FluentValidation;
using SmkDoc.Application.Modules.IdentityAccess.Projects.Commands.CreateProject;
using SmkDoc.Application.Modules.IdentityAccess.Projects.DTOs;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Enums;
using SmkDoc.Domain.Exceptions;
using SmkDoc.Domain.Interfaces;
using SmkDoc.Domain.ValueObjects;
using SmkDoc.Tests.Common.Builders;
using ValidationException = SmkDoc.Application.Common.Exceptions.ValidationException;

namespace SmkDoc.Tests.Application.Modules.IdentityAccess.Projects.Commands.CreateProject;

public sealed class CreateProjectUseCaseTests
{
    private readonly IProjectRepository _projectRepo = Substitute.For<IProjectRepository>();
    private readonly IUserProjectRoleRepository _roleRepo = Substitute.For<IUserProjectRoleRepository>();
    private readonly ICompanyRepository _companyRepo = Substitute.For<ICompanyRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly FakeTimeProvider _timeProvider = TestConstants.CreateFakeClock();

    public CreateProjectUseCaseTests()
    {
        _unitOfWork.CommitAsync(Arg.Any<CancellationToken>()).Returns(1);
    }

    private CreateProjectUseCase CreateSut(IValidator<CreateProjectCommand>? validator = null) =>
        new(
            _projectRepo,
            _roleRepo,
            _companyRepo,
            _unitOfWork,
            validator,
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
        var act = () => CreateSut().ExecuteAsync(command);

        // Assert
        await act.Should().ThrowAsync<ConflictException>();
        await _projectRepo.DidNotReceive().AddAsync(Arg.Any<Project>(), Arg.Any<CancellationToken>());
        await _unitOfWork.DidNotReceive().CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenValidCommand_CreatesProjectAndAssignsAdminRole()
    {
        // Arrange
        var now = TestConstants.BaselineTime;
        var userId = Guid.NewGuid();
        Company company = new CompanyBuilder().WithName("Test Company").WithTime(now);
        var command = new CreateProjectCommand(userId, "New Project", "new-project");

        _projectRepo.GetBySlugAsync("new-project", Arg.Any<CancellationToken>())
            .Returns((Project?)null);
        _companyRepo.GetFirstAsync(Arg.Any<CancellationToken>())
            .Returns(company);

        Project? capturedProject = null;
        _projectRepo.AddAsync(Arg.Do<Project>(p => capturedProject = p), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        UserProjectRole? capturedRole = null;
        _roleRepo.AddAsync(Arg.Do<UserProjectRole>(r => capturedRole = r), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        // Act
        ProjectResultDto result = await CreateSut().ExecuteAsync(command);

        // Assert
        result.Should().NotBeNull();
        result.Name.Should().Be("New Project");
        result.Slug.Should().Be("new-project");

        capturedProject.Should().NotBeNull();
        capturedRole.Should().NotBeNull();
        capturedRole!.UserId.Should().Be(userId);
        capturedRole.Role.Should().Be(RoleType.Admin);

        await _unitOfWork.Received(1).CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenNoCompanyExists_CreatesDefaultCompanyAndCommitsOnceAtomically()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var command = new CreateProjectCommand(userId, "Fallback Project", "fallback-project");

        _projectRepo.GetBySlugAsync("fallback-project", Arg.Any<CancellationToken>())
            .Returns((Project?)null);
        _companyRepo.GetFirstAsync(Arg.Any<CancellationToken>())
            .Returns((Company?)null);

        Company? capturedCompany = null;
        _companyRepo.AddAsync(Arg.Do<Company>(c => capturedCompany = c), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        // Act
        ProjectResultDto result = await CreateSut().ExecuteAsync(command);

        // Assert
        result.Should().NotBeNull();
        capturedCompany.Should().NotBeNull();
        capturedCompany!.Name.Value.Should().Be("Default Company");
        await _companyRepo.Received(1).AddAsync(Arg.Any<Company>(), Arg.Any<CancellationToken>());
        await _unitOfWork.Received(1).CommitAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExecuteAsync_WhenValidationFails_ThrowsValidationExceptionAndDoesNotPersist()
    {
        // Arrange
        var command = new CreateProjectCommand(Guid.Empty, "", "");
        var validator = new CreateProjectCommandValidator();

        // Act
        var act = () => CreateSut(validator).ExecuteAsync(command);

        // Assert
        await act.Should().ThrowAsync<ValidationException>();
        await _projectRepo.DidNotReceive().AddAsync(Arg.Any<Project>(), Arg.Any<CancellationToken>());
        await _unitOfWork.DidNotReceive().CommitAsync(Arg.Any<CancellationToken>());
    }
}
