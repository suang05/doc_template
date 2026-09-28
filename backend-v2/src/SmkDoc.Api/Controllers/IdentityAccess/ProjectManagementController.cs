using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmkDoc.Api.Models;
using SmkDoc.Application.DTOs.Projects;
using SmkDoc.Application.UseCases.Projects;
using SmkDoc.Api.Extensions;

namespace SmkDoc.Api.Controllers;

/// <summary>
/// Project management — list and create projects.
/// Auth: JWT Bearer (JwtPolicy) — Admin role required for mutating operations.
/// </summary>
[ApiController]
[Route("api/v1/management/projects")]
[Authorize(Policy = "JwtPolicy")]
public class ProjectManagementController(ProjectManagementUseCase useCase) : ControllerBase
{
    /// <summary>List all projects accessible to the authenticated user.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<IEnumerable<ProjectListItemDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListProjects(CancellationToken ct)
    {
        var userId = User.GetUserId();
        var projects = await useCase.ListProjectsAsync(userId, ct);
        var result = projects.Select(p => new ProjectListItemDto(p.Id, p.Name, p.Slug, p.IsActive, p.CreatedAt));
        return Ok(new ApiResponse<IEnumerable<ProjectListItemDto>>(result));
    }

    /// <summary>Create a new project. Admin only.</summary>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateProject(
        [FromBody] CreateProjectRequest req,
        CancellationToken ct)
    {
        User.RequireAdmin();
        var userId = User.GetUserId();
        var project = await useCase.CreateProjectAsync(userId, req.Name, req.Slug, ct);

        var responseDto = new ProjectDto(project.Id, project.Name, project.Slug, project.IsActive, project.CreatedAt);
        return CreatedAtAction(nameof(ListProjects), new { id = project.Id }, new ApiResponse<ProjectDto>(responseDto));
    }
}
