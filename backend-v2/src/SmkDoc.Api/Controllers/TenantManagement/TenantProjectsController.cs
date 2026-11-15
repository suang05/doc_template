using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SmkDoc.Api.Binders;
using SmkDoc.Api.Common.Responses;
using SmkDoc.Api.Constants;
using SmkDoc.Api.Contracts.TenantManagement;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.IdentityAccess.Projects.DTOs;
using SmkDoc.Application.Modules.TenantManagement.Projects.Commands.ActivateProject;
using SmkDoc.Application.Modules.TenantManagement.Projects.Commands.DeactivateProject;
using SmkDoc.Application.Modules.TenantManagement.Projects.Commands.UpdateProjectEgressCidrs;
using SmkDoc.Application.Modules.TenantManagement.Projects.DTOs;
using SmkDoc.Application.Modules.TenantManagement.Projects.Queries.ListControlPlaneProjects;

namespace SmkDoc.Api.Controllers.TenantManagement;

/// <summary>
/// SuperAdmin Control Plane governance for tenant lifecycle activation, suspension, and outbound egress CIDRs.
/// </summary>
[ApiController]
[Route("api/v1/management")]
[Authorize(Policy = AuthorizationPolicies.RequireSuperAdmin)]
[EnableRateLimiting("api")]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
[ProducesResponseType(StatusCodes.Status403Forbidden)]
public sealed class TenantProjectsController(
    IUseCase<ListControlPlaneProjectsQuery, ProjectPagedResultDto> listControlPlaneProjectsUseCase,
    IUseCase<ActivateProjectCommand, ProjectControlPlaneResultDto> activateProjectUseCase,
    IUseCase<DeactivateProjectCommand, ProjectControlPlaneResultDto> deactivateProjectUseCase,
    IUseCase<UpdateProjectEgressCidrsCommand, ProjectControlPlaneResultDto> updateEgressCidrsUseCase) : ControllerBase
{
    /// <summary>List all tenant projects across the platform in Control Plane metadata scope.</summary>
    [HttpGet("tenants")]
    [ProducesResponseType(typeof(PagedApiResponse<ProjectResultDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListAllTenants(
        [FromQuery] string? search = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var query = new ListControlPlaneProjectsQuery(search, page, pageSize);
        var result = await listControlPlaneProjectsUseCase.ExecuteAsync(query, ct);
        return Ok(new PagedApiResponse<ProjectResultDto>(result.Items, result.TotalCount, result.Page, result.PageSize));
    }

    /// <summary>Activate a suspended tenant project.</summary>
    [HttpPost("projects/{projectId:guid}/activate")]
    [ProducesResponseType(typeof(ApiResponse<ProjectControlPlaneResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status412PreconditionFailed)]
    [ProducesResponseType(StatusCodes.Status428PreconditionRequired)]
    public async Task<IActionResult> ActivateProject(
        [FromRoute] Guid projectId,
        [FromIfMatch] long expectedVersion,
        CancellationToken ct)
    {
        var command = new ActivateProjectCommand(projectId, expectedVersion);
        var result = await activateProjectUseCase.ExecuteAsync(command, ct);
        return Ok(new ApiResponse<ProjectControlPlaneResultDto>(result));
    }

    /// <summary>Deactivate (suspend) a tenant project immediately.</summary>
    [HttpPost("projects/{projectId:guid}/deactivate")]
    [ProducesResponseType(typeof(ApiResponse<ProjectControlPlaneResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status412PreconditionFailed)]
    [ProducesResponseType(StatusCodes.Status428PreconditionRequired)]
    public async Task<IActionResult> DeactivateProject(
        [FromRoute] Guid projectId,
        [FromIfMatch] long expectedVersion,
        CancellationToken ct)
    {
        var command = new DeactivateProjectCommand(projectId, expectedVersion);
        var result = await deactivateProjectUseCase.ExecuteAsync(command, ct);
        return Ok(new ApiResponse<ProjectControlPlaneResultDto>(result));
    }

    /// <summary>Update approved outbound private/RFC-1918 egress CIDRs for a tenant project.</summary>
    [HttpPut("projects/{projectId:guid}/egress-cidrs")]
    [ProducesResponseType(typeof(ApiResponse<ProjectControlPlaneResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status412PreconditionFailed)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(StatusCodes.Status428PreconditionRequired)]
    public async Task<IActionResult> UpdateEgressCidrs(
        [FromRoute] Guid projectId,
        [FromIfMatch] long expectedVersion,
        [FromBody] UpdateProjectEgressCidrsRequest request,
        CancellationToken ct)
    {
        var command = new UpdateProjectEgressCidrsCommand(projectId, request.AllowedCidrs ?? [], expectedVersion);
        var result = await updateEgressCidrsUseCase.ExecuteAsync(command, ct);
        return Ok(new ApiResponse<ProjectControlPlaneResultDto>(result));
    }
}
