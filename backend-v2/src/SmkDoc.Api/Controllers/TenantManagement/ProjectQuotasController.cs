using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SmkDoc.Api.Binders;
using SmkDoc.Api.Common.Responses;
using SmkDoc.Api.Constants;
using SmkDoc.Api.Contracts.TenantManagement;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.TenantManagement.Quotas.Commands.UpsertProjectQuota;
using SmkDoc.Application.Modules.TenantManagement.Quotas.DTOs;
using SmkDoc.Application.Modules.TenantManagement.Quotas.Queries.GetProjectQuota;

namespace SmkDoc.Api.Controllers.TenantManagement;

/// <summary>
/// SuperAdmin Control Plane management of per-tenant operational quotas and concurrency limits.
/// </summary>
[ApiController]
[Route("api/v1/management/projects/{projectId:guid}/quota")]
[Authorize(Policy = AuthorizationPolicies.RequireSuperAdmin)]
[EnableRateLimiting("api")]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
[ProducesResponseType(StatusCodes.Status403Forbidden)]
public sealed class ProjectQuotasController(
    IUseCase<GetProjectQuotaQuery, ProjectQuotaResultDto> getQuotaUseCase,
    IUseCase<UpsertProjectQuotaCommand, ProjectQuotaResultDto> upsertQuotaUseCase) : ControllerBase
{
    /// <summary>Retrieve effective operational quota limits for a tenant project.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<ProjectQuotaResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetQuota([FromRoute] Guid projectId, CancellationToken ct)
    {
        var query = new GetProjectQuotaQuery(projectId);
        var result = await getQuotaUseCase.ExecuteAsync(query, ct);
        return Ok(new ApiResponse<ProjectQuotaResultDto>(result));
    }

    /// <summary>Create or update operational quota limits for a tenant project with optimistic concurrency.</summary>
    [HttpPut]
    [ProducesResponseType(typeof(ApiResponse<ProjectQuotaResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status412PreconditionFailed)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(StatusCodes.Status428PreconditionRequired)]
    public async Task<IActionResult> UpsertQuota(
        [FromRoute] Guid projectId,
        [FromIfMatch] long expectedVersion,
        [FromBody] UpsertProjectQuotaRequest request,
        CancellationToken ct)
    {
        var command = new UpsertProjectQuotaCommand(
            projectId,
            request.MaxRequestsPerMinute,
            request.MaxConcurrentJobs,
            request.MaxBatchItems,
            request.MaxStorageBytes,
            request.MaxActiveApiKeys,
            request.IsActive,
            expectedVersion);
        var result = await upsertQuotaUseCase.ExecuteAsync(command, ct);
        return Ok(new ApiResponse<ProjectQuotaResultDto>(result));
    }
}
