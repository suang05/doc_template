using SmkDoc.Application.Modules.IdentityAccess.Projects.Queries.GetProjectById;
using SmkDoc.Domain.Entities;
using SmkDoc.Tests.Common.Builders;

namespace SmkDoc.Tests.Application.Modules.IdentityAccess.Projects.Queries.GetProjectById;

public class GetProjectByIdUseCaseTests
{
    private readonly Mock<IProjectRepository> _projectRepoMock = new();

    private GetProjectByIdUseCase CreateSut() => new(_projectRepoMock.Object);

    [Fact]
    public async Task ExecuteAsync_WhenProjectNotFound_ThrowsNotFoundException()
    {
        // Arrange
        var projectId = Guid.NewGuid();
        _projectRepoMock.Setup(r => r.GetByIdAsync(projectId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Project?)null);

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
        var project = new ProjectBuilder()
            .WithId(projectId)
            .WithName("Billing Project")
            .WithSlug("billing-proj")
            .Build();

        _projectRepoMock.Setup(r => r.GetByIdAsync(projectId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(project);

        // Act
        var result = await CreateSut().ExecuteAsync(new GetProjectByIdQuery(projectId));

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be(projectId);
        result.Name.Should().Be("Billing Project");
        result.Slug.Should().Be("billing-proj");
    }
}
