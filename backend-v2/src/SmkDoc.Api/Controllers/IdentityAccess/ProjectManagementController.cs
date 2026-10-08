using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmkDoc.Api.Common.Responses;
using SmkDoc.Api.Contracts.IdentityAccess.Projects;
using SmkDoc.Api.Extensions;
using SmkDoc.Application.Modules.IdentityAccess.Projects.Commands.CreateProject;
using SmkDoc.Application.Modules.IdentityAccess.Projects.DTOs;
using SmkDoc.Application.Modules.IdentityAccess.Projects.Queries.GetProjectById;
using SmkDoc.Application.Modules.IdentityAccess.Projects.Queries.ListProjects;

namespace SmkDoc.Api.Controllers.IdentityAccess;

/// <summary>
/// Project management — list, view, and create tenant projects.
/// </summary>
[ApiController]
[Route("api/v1/management/projects")]
[Authorize]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
public class ProjectManagementController(
    ListProjectsUseCase listProjectsUseCase,
    GetProjectByIdUseCase getProjectByIdUseCase,
    CreateProjectUseCase createProjectUseCase) : ControllerBase
{
    /// <summary>List all projects accessible to the authenticated user with server-side pagination and search.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedApiResponse<ProjectResultDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListProjects(
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var userId = User.GetUserId();
        var query = new ListProjectsQuery(userId, search, page, pageSize);
        var result = await listProjectsUseCase.ExecuteAsync(query, ct);

        return Ok(new PagedApiResponse<ProjectResultDto>(result.Items, result.TotalCount, result.Page, result.PageSize));
    }

    /// <summary>Get a specific project by its ID.</summary>
    [HttpGet("{projectId:guid}")]
    [ProducesResponseType(typeof(ApiResponse<ProjectResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetProjectById([FromRoute] Guid projectId, CancellationToken ct)
    {
        var query = new GetProjectByIdQuery(projectId);
        var project = await getProjectByIdUseCase.ExecuteAsync(query, ct);
        if (project is null)
        {
            return NotFound();
        }

        return Ok(new ApiResponse<ProjectResultDto>(project));
    }

    /// <summary>Create a new project. Admin only.</summary>
    [HttpPost]
    [Authorize(Roles = "Admin")] // Declarative RBAC คืน 403 Forbidden อัตโนมัติเมื่อไม่มีสิทธิ์
    [ProducesResponseType(typeof(ApiResponse<ProjectResultDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateProject(
        [FromBody] CreateProjectRequest req,
        CancellationToken ct)
    {
        var userId = User.GetUserId();
        var command = new CreateProjectCommand(userId, req.Name, req.Slug);
        var project = await createProjectUseCase.ExecuteAsync(command, ct);

        // ชี้ Location Header ไปยัง GetProjectById ที่สร้างขึ้นจริงอย่างถูกต้องตาม RFC 7231
        return CreatedAtAction(
            nameof(GetProjectById),
            new { projectId = project.Id },
            new ApiResponse<ProjectResultDto>(project));
    }
}