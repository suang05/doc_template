using FluentAssertions;
using Moq;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.IdentityAccess.Projects.Commands.CreateProject;
using SmkDoc.Application.Modules.IdentityAccess.Projects.Queries.GetProjectById;
using SmkDoc.Application.Modules.IdentityAccess.Projects.Queries.ListProjects;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Enums;
using SmkDoc.Domain.Exceptions;
using SmkDoc.Domain.Interfaces;
using Xunit;

namespace SmkDoc.Tests.Application.Modules.IdentityAccess.Projects;

public class ProjectUseCaseTests
{
    private readonly Mock<IProjectRepository> _projectRepoMock = new();
    private readonly Mock<IUserProjectRoleRepository> _roleRepoMock = new();
    private readonly Mock<ICompanyRepository> _companyRepoMock = new();
    private readonly Mock<IUnitOfWork> _uowMock = new();

    [Fact]
    public async Task CreateProject_WhenSlugExists_ThrowsConflictException()
    {
        // Arrange
        var command = new CreateProjectCommand(Guid.NewGuid(), "Existing Project", "existing-slug");
        var existingProject = new Project(Guid.NewGuid(), "Existing Project", "existing-slug");

        _projectRepoMock.Setup(r => r.GetBySlugAsync("existing-slug", It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingProject);

        var useCase = new CreateProjectUseCase(
            _projectRepoMock.Object,
            _roleRepoMock.Object,
            _companyRepoMock.Object,
            _uowMock.Object);

        // Act & Assert
        Func<Task> act = () => useCase.ExecuteAsync(command);
        await act.Should().ThrowAsync<ConflictException>();
    }

    [Fact]
    public async Task CreateProject_WhenValid_CreatesProjectAndAssignsAdminRole()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var company = new Company("Test Company");
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

        var useCase = new CreateProjectUseCase(
            _projectRepoMock.Object,
            _roleRepoMock.Object,
            _companyRepoMock.Object,
            _uowMock.Object);

        // Act
        var result = await useCase.ExecuteAsync(command);

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
    public async Task ListProjects_ReturnsOnlyActiveProjectsForUser()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var p1 = new Project(Guid.NewGuid(), "Proj 1", "proj-1");
        var p2 = new Project(Guid.NewGuid(), "Proj 2", "proj-2");
        p2.Deactivate();

        var roles = new List<UserProjectRole>
        {
            new(userId, p1.Id, RoleType.Admin),
            new(userId, p2.Id, RoleType.Viewer)
        };

        _roleRepoMock.Setup(r => r.ListByUserAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(roles);
        _projectRepoMock.Setup(r => r.ListByIdsAsync(It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Project> { p1, p2 });

        var useCase = new ListProjectsUseCase(_projectRepoMock.Object, _roleRepoMock.Object);

        // Act
        var result = (await useCase.ExecuteAsync(new ListProjectsQuery(userId))).ToList();

        // Assert
        result.Should().HaveCount(1);
        result[0].Id.Should().Be(p1.Id);
    }

    [Fact]
    public async Task GetProjectById_WhenNotFound_ThrowsNotFoundException()
    {
        // Arrange
        var projectId = Guid.NewGuid();
        _projectRepoMock.Setup(r => r.GetByIdAsync(projectId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Project?)null);

        var useCase = new GetProjectByIdUseCase(_projectRepoMock.Object);

        // Act & Assert
        Func<Task> act = () => useCase.ExecuteAsync(new GetProjectByIdQuery(projectId));
        await act.Should().ThrowAsync<NotFoundException>();
    }
}
