using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SmkDoc.Api.Common.Responses;
using SmkDoc.Api.Constants;
using SmkDoc.Api.Contracts.IdentityAccess.Projects;
using SmkDoc.Api.Extensions;
using SmkDoc.Api.Filters;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.IdentityAccess.Projects.Commands.CreateProject;
using SmkDoc.Application.Modules.IdentityAccess.Projects.Commands.TransferProjectOwner;
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
[EnableRateLimiting("api")]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
public sealed class ProjectManagementController(
    IUseCase<ListProjectsQuery, ProjectPagedResultDto> listProjectsUseCase,
    IUseCase<GetProjectByIdQuery, ProjectResultDto> getProjectByIdUseCase,
    IUseCase<CreateProjectCommand, ProjectResultDto> createProjectUseCase,
    IUseCase<TransferProjectOwnerCommand> transferOwnerUseCase) : ControllerBase
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
        var pagedResult = await listProjectsUseCase.ExecuteAsync(query, ct);

        var result = new PagedApiResponse<ProjectResultDto>(pagedResult.Items, pagedResult.TotalCount, pagedResult.Page, pagedResult.PageSize);
        return Ok(result);
    }

    /// <summary>Get a specific project by its ID.</summary>
    [HttpGet("{projectId:guid}")]
    [ProducesResponseType(typeof(ApiResponse<ProjectResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetProjectById([FromRoute] Guid projectId, CancellationToken ct)
    {
        var query = new GetProjectByIdQuery(projectId);
        var result = await getProjectByIdUseCase.ExecuteAsync(query, ct);
        if (result is null)
        {
            return NotFound();
        }

        return Ok(new ApiResponse<ProjectResultDto>(result));
    }

    /// <summary>Create a new project. SuperAdmin only.</summary>
    [HttpPost]
    [Authorize(Policy = SmkDocPolicies.RequireSuperAdmin)]
    [ServiceFilter(typeof(IdempotencyFilter))]
    [ProducesResponseType(typeof(ApiResponse<ProjectResultDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateProject(
        [FromBody] CreateProjectRequest request,
        CancellationToken ct)
    {
        var userId = User.GetUserId();
        var command = new CreateProjectCommand(userId, request.Name, request.Slug);
        var result = await createProjectUseCase.ExecuteAsync(command, ct);

        // ชี้ Location Header ไปยัง GetProjectById ที่สร้างขึ้นจริงอย่างถูกต้องตาม RFC 7231
        return CreatedAtAction(
            nameof(GetProjectById),
            new { projectId = result.Id },
            new ApiResponse<ProjectResultDto>(result));
    }

    /// <summary>Transfer project ownership / Master Credential authority to a new IT Lead. SuperAdmin only.</summary>
    [HttpPost("{projectId:guid}/transfer-owner")]
    [Authorize(Policy = SmkDocPolicies.RequireSuperAdmin)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> TransferOwner(
        [FromRoute] Guid projectId,
        [FromBody] TransferProjectOwnerRequest request,
        CancellationToken ct)
    {
        var command = new TransferProjectOwnerCommand(projectId, request.NewOwnerUserId);
        await transferOwnerUseCase.ExecuteAsync(command, ct);
        return NoContent();
    }
}
