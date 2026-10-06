using FluentValidation;
using ValidationException = SmkDoc.Application.Common.Exceptions.ValidationException;
using SmkDoc.Application.Modules.IdentityAccess.Projects.Commands.CreateProject;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Enums;
using SmkDoc.Domain.Exceptions;
using SmkDoc.Domain.ValueObjects;
using SmkDoc.Tests.Common.Builders;
using SmkDoc.Tests.Common.Factories;

namespace SmkDoc.Tests.Application.Modules.IdentityAccess.Projects.Commands.CreateProject;

public class CreateProjectUseCaseTests
{
    private readonly Mock<IProjectRepository> _projectRepoMock = new();
    private readonly Mock<IUserProjectRoleRepository> _roleRepoMock = new();
    private readonly Mock<ICompanyRepository> _companyRepoMock = new();
    private readonly Mock<IUnitOfWork> _uowMock = new();

    private CreateProjectUseCase CreateSut(IValidator<CreateProjectCommand>? validator = null) =>
        new(
            _projectRepoMock.Object,
            _roleRepoMock.Object,
            _companyRepoMock.Object,
            _uowMock.Object,
            validator);

    [Fact]
    public async Task ExecuteAsync_WhenSlugAlreadyExists_ThrowsConflictException()
    {
        // Arrange
        var now = TestConstants.BaselineTime;
        var command = new CreateProjectCommand(Guid.NewGuid(), "Existing Project", "existing-slug");
        var existingProject = ProjectBuilder.AProject()
            .WithName("Existing Project")
            .WithSlug("existing-slug")
            .WithTime(now)
            .Build();

        _projectRepoMock.Setup(r => r.GetBySlugAsync("existing-slug", It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingProject);

        // Act
        var act = () => CreateSut().ExecuteAsync(command);

        // Assert
        await act.Should().ThrowAsync<ConflictException>();
        _projectRepoMock.Verify(r => r.AddAsync(It.IsAny<Project>(), It.IsAny<CancellationToken>()), Times.Never);
        _uowMock.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_WhenValidCommand_CreatesProjectAndAssignsAdminRole()
    {
        // Arrange
        var now = TestConstants.BaselineTime;
        var userId = Guid.NewGuid();
        var company = CompanyTestFactory.Create(name: "Test Company", now: now);
        var command = new CreateProjectCommand(userId, "New Project", "new-project");

        _projectRepoMock.Setup(r => r.GetBySlugAsync("new-project", It.IsAny<CancellationToken>()))
            .ReturnsAsync((Project?)null);
        _companyRepoMock.Setup(r => r.GetFirstAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(company);

        Project? capturedProject = null;
        _projectRepoMock.Setup(r => r.AddAsync(It.IsAny<Project>(), It.IsAny<CancellationToken>()))
            .Callback<Project, CancellationToken>((p, _) => capturedProject = p)
            .Returns(Task.CompletedTask);

        UserProjectRole? capturedRole = null;
        _roleRepoMock.Setup(r => r.AddAsync(It.IsAny<UserProjectRole>(), It.IsAny<CancellationToken>()))
            .Callback<UserProjectRole, CancellationToken>((r, _) => capturedRole = r)
            .Returns(Task.CompletedTask);

        // Act
        var result = await CreateSut().ExecuteAsync(command);

        // Assert
        result.Should().NotBeNull();
        result.Name.Should().Be("New Project");
        result.Slug.Should().Be("new-project");

        capturedProject.Should().NotBeNull();
        capturedRole.Should().NotBeNull();
        capturedRole!.UserId.Should().Be(userId);
        capturedRole.Role.Should().Be(RoleType.Admin);

        _uowMock.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_WhenNoCompanyExists_CreatesDefaultCompanyAndCommitsTwice()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var command = new CreateProjectCommand(userId, "Fallback Project", "fallback-project");

        _projectRepoMock.Setup(r => r.GetBySlugAsync("fallback-project", It.IsAny<CancellationToken>()))
            .ReturnsAsync((Project?)null);
        _companyRepoMock.Setup(r => r.GetFirstAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((Company?)null);

        Company? capturedCompany = null;
        _companyRepoMock.Setup(r => r.AddAsync(It.IsAny<Company>(), It.IsAny<CancellationToken>()))
            .Callback<Company, CancellationToken>((c, _) => capturedCompany = c)
            .Returns(Task.CompletedTask);

        // Act
        var result = await CreateSut().ExecuteAsync(command);

        // Assert
        result.Should().NotBeNull();
        capturedCompany.Should().NotBeNull();
        capturedCompany!.Name.Value.Should().Be("Default Company");
        _companyRepoMock.Verify(r => r.AddAsync(It.IsAny<Company>(), It.IsAny<CancellationToken>()), Times.Once);
        _uowMock.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Exactly(2));
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
        _projectRepoMock.Verify(r => r.AddAsync(It.IsAny<Project>(), It.IsAny<CancellationToken>()), Times.Never);
        _uowMock.Verify(u => u.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
