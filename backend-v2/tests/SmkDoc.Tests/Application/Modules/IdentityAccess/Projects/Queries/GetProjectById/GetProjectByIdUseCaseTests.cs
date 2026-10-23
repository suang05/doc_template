using SmkDoc.Application.Modules.IdentityAccess.Projects.DTOs;
using SmkDoc.Application.Modules.IdentityAccess.Projects.Queries.GetProjectById;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Exceptions;
using SmkDoc.Domain.Interfaces;
using SmkDoc.Domain.ValueObjects.Ids;
using SmkDoc.Tests.Common.Builders;

namespace SmkDoc.Tests.Application.Modules.IdentityAccess.Projects.Queries.GetProjectById;

public sealed class GetProjectByIdUseCaseTests
{
    private readonly IProjectRepository _projectRepo = Substitute.For<IProjectRepository>();

    private GetProjectByIdUseCase CreateSut() => new(_projectRepo);

    [Fact]
    public async Task ExecuteAsync_WhenProjectNotFound_ThrowsNotFoundException()
    {
        // Arrange
        var projectId = Guid.NewGuid();
        _projectRepo.GetByIdAsync(new ProjectId(projectId), Arg.Any<CancellationToken>())
            .Returns((Project?)null);

        // Act
        var act = () => CreateSut().ExecuteAsync(new GetProjectByIdQuery(projectId));

        // Assert
        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task ExecuteAsync_WhenProjectExists_ReturnsProjectDto()
    {
        // Arrange
        var projectId = Guid.NewGuid();
        Project project = ProjectBuilder.AProject()
            .WithId(projectId)
            .WithName("Billing Project")
            .WithSlug("billing-proj");

        _projectRepo.GetByIdAsync(new ProjectId(projectId), Arg.Any<CancellationToken>())
            .Returns(project);

        // Act
        ProjectResultDto result = await CreateSut().ExecuteAsync(new GetProjectByIdQuery(projectId));

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be(projectId);
        result.Name.Should().Be("Billing Project");
        result.Slug.Should().Be("billing-proj");
    }
}
