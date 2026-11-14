using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SmkDoc.Api.Common.Responses;
using SmkDoc.Api.Contracts.IdentityAccess.Projects;
using SmkDoc.Api.Controllers.IdentityAccess;
using SmkDoc.Application.Modules.IdentityAccess.Projects.Commands.CreateProject;
using SmkDoc.Application.Modules.IdentityAccess.Projects.Commands.TransferProjectOwner;
using SmkDoc.Application.Modules.IdentityAccess.Projects.DTOs;
using SmkDoc.Application.Modules.IdentityAccess.Projects.Queries.GetProjectById;
using SmkDoc.Application.Modules.IdentityAccess.Projects.Queries.ListProjects;

namespace SmkDoc.Tests.Api.Controllers;

public sealed class ProjectsControllerTests
{
    private readonly IUseCase<ListProjectsQuery, ProjectPagedResultDto> _listUseCase =
        Substitute.For<IUseCase<ListProjectsQuery, ProjectPagedResultDto>>();
    private readonly IUseCase<GetProjectByIdQuery, ProjectResultDto> _getByIdUseCase =
        Substitute.For<IUseCase<GetProjectByIdQuery, ProjectResultDto>>();
    private readonly IUseCase<CreateProjectCommand, ProjectResultDto> _createUseCase =
        Substitute.For<IUseCase<CreateProjectCommand, ProjectResultDto>>();
    private readonly IUseCase<TransferProjectOwnerCommand> _transferOwnerUseCase =
        Substitute.For<IUseCase<TransferProjectOwnerCommand>>();

    private readonly Guid _currentUserId = Guid.NewGuid();

    private ProjectsController CreateSut()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, _currentUserId.ToString())],
            "TestAuth"));

        return new ProjectsController(
            _listUseCase,
            _getByIdUseCase,
            _createUseCase,
            _transferOwnerUseCase)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = httpContext
            }
        };
    }

    [Fact]
    public async Task ListProjects_InvokesUseCaseWithCurrentUserId_ReturnsPagedApiResponse()
    {
        // Arrange
        var pagedDto = new ProjectPagedResultDto(
            [new ProjectResultDto(Guid.NewGuid(), "Project Alpha", "project-alpha", true, DateTimeOffset.UtcNow, null, null, "50001")],
            TotalCount: 1,
            Page: 1,
            PageSize: 20);

        _listUseCase.ExecuteAsync(Arg.Any<ListProjectsQuery>(), Arg.Any<CancellationToken>())
            .Returns(pagedDto);

        var controller = CreateSut();

        // Act
        var result = await controller.ListProjects(search: "Alpha", page: 1, pageSize: 20, CancellationToken.None);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<PagedApiResponse<ProjectResultDto>>().Subject;
        response.Data.Should().HaveCount(1);
        response.Pagination.TotalCount.Should().Be(1);

        await _listUseCase.Received(1).ExecuteAsync(
            Arg.Is<ListProjectsQuery>(q => q.UserId == _currentUserId && q.SearchTerm == "Alpha" && q.Page == 1 && q.PageSize == 20),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetProjectById_ReturnsOk()
    {
        // Arrange
        var projectId = Guid.NewGuid();
        var projectDto = new ProjectResultDto(
            projectId,
            "Target Project",
            "target-project",
            true,
            DateTimeOffset.UtcNow,
            null,
            null,
            "54321");

        _getByIdUseCase.ExecuteAsync(Arg.Any<GetProjectByIdQuery>(), Arg.Any<CancellationToken>())
            .Returns(projectDto);

        var controller = CreateSut();

        // Act
        var result = await controller.GetProjectById(projectId, CancellationToken.None);

        // Assert
        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<ApiResponse<ProjectResultDto>>().Subject;
        response.Data.Id.Should().Be(projectId);

        await _getByIdUseCase.Received(1).ExecuteAsync(
            Arg.Is<GetProjectByIdQuery>(q => q.ProjectId == projectId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateProject_WithValidRequest_ReturnsCreatedAtAction()
    {
        // Arrange
        var projectId = Guid.NewGuid();
        var createdDto = new ProjectResultDto(
            projectId,
            "New Project",
            "new-project",
            true,
            DateTimeOffset.UtcNow,
            "read-key",
            "write-key",
            "10001");

        _createUseCase.ExecuteAsync(Arg.Any<CreateProjectCommand>(), Arg.Any<CancellationToken>())
            .Returns(createdDto);

        var controller = CreateSut();
        var request = new CreateProjectRequest("New Project", "new-project");

        // Act
        var result = await controller.CreateProject(request, CancellationToken.None);

        // Assert
        var createdResult = result.Should().BeOfType<CreatedAtActionResult>().Subject;
        createdResult.StatusCode.Should().Be(StatusCodes.Status201Created);
        createdResult.ActionName.Should().Be(nameof(ProjectsController.GetProjectById));
        createdResult.RouteValues!["projectId"].Should().Be(projectId);
        var response = createdResult.Value.Should().BeOfType<ApiResponse<ProjectResultDto>>().Subject;
        response.Data.Name.Should().Be("New Project");

        await _createUseCase.Received(1).ExecuteAsync(
            Arg.Is<CreateProjectCommand>(c => c.UserId == _currentUserId && c.Name == "New Project" && c.Slug == "new-project"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task TransferOwner_ReturnsNoContent()
    {
        // Arrange
        var projectId = Guid.NewGuid();
        var newOwnerId = Guid.NewGuid();

        _transferOwnerUseCase.ExecuteAsync(Arg.Any<TransferProjectOwnerCommand>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var controller = CreateSut();
        var request = new TransferProjectOwnerRequest(newOwnerId);

        // Act
        var result = await controller.TransferOwner(projectId, request, CancellationToken.None);

        // Assert
        result.Should().BeOfType<NoContentResult>();
        await _transferOwnerUseCase.Received(1).ExecuteAsync(
            Arg.Is<TransferProjectOwnerCommand>(c => c.ProjectId == projectId && c.NewOwnerUserId == newOwnerId),
            Arg.Any<CancellationToken>());
    }
}

