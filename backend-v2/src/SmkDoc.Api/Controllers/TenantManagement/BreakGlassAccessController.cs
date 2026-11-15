using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SmkDoc.Api.Binders;
using SmkDoc.Api.Common.Responses;
using SmkDoc.Api.Constants;
using SmkDoc.Api.Contracts.TenantManagement;
using SmkDoc.Api.Extensions;
using SmkDoc.Api.Filters;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.TenantManagement.BreakGlass.Commands.GrantBreakGlassAccess;
using SmkDoc.Application.Modules.TenantManagement.BreakGlass.Commands.RevokeBreakGlassAccess;
using SmkDoc.Application.Modules.TenantManagement.BreakGlass.DTOs;
using SmkDoc.Application.Modules.TenantManagement.BreakGlass.Queries.GetBreakGlassGrantById;
using SmkDoc.Application.Modules.TenantManagement.BreakGlass.Queries.ListBreakGlassGrants;

namespace SmkDoc.Api.Controllers.TenantManagement;

/// <summary>
/// SuperAdmin Control Plane Zero-Standing-Access (ZSA) break-glass grant lifecycle management.
/// </summary>
[ApiController]
[Route("api/v1/management/break-glass-grants")]
[Authorize(Policy = AuthorizationPolicies.RequireSuperAdmin)]
[EnableRateLimiting("api")]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
[ProducesResponseType(StatusCodes.Status403Forbidden)]
public sealed class BreakGlassAccessController(
    IUseCase<ListBreakGlassGrantsQuery, IReadOnlyList<BreakGlassGrantResultDto>> listGrantsUseCase,
    IUseCase<GetBreakGlassGrantByIdQuery, BreakGlassGrantResultDto> getGrantByIdUseCase,
    IUseCase<GrantBreakGlassAccessCommand, BreakGlassGrantResultDto> grantAccessUseCase,
    IUseCase<RevokeBreakGlassAccessCommand> revokeAccessUseCase) : ControllerBase
{
    /// <summary>List active time-boxed break-glass grants optionally filtered by SuperAdmin or project.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<BreakGlassGrantResultDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListGrants(
        [FromQuery] Guid? superAdminUserId = null,
        [FromQuery] Guid? targetProjectId = null,
        CancellationToken ct = default)
    {
        var query = new ListBreakGlassGrantsQuery(superAdminUserId, targetProjectId);
        var result = await listGrantsUseCase.ExecuteAsync(query, ct);
        return Ok(new ApiResponse<IReadOnlyList<BreakGlassGrantResultDto>>(result));
    }

    /// <summary>Get a single break-glass grant by ID along with its current ETag.</summary>
    [HttpGet("{grantId:guid}")]
    [ProducesResponseType(typeof(ApiResponse<BreakGlassGrantResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById([FromRoute] Guid grantId, CancellationToken ct)
    {
        var query = new GetBreakGlassGrantByIdQuery(grantId);
        var result = await getGrantByIdUseCase.ExecuteAsync(query, ct);
        return Ok(new ApiResponse<BreakGlassGrantResultDto>(result));
    }

    /// <summary>Issue a time-boxed audited break-glass access grant into a tenant project.</summary>
    [HttpPost]
    [Idempotent]
    [ProducesResponseType(typeof(ApiResponse<BreakGlassGrantResultDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> GrantAccess(
        [FromBody] GrantBreakGlassAccessRequest request,
        CancellationToken ct)
    {
        var granterUserId = User.GetUserId();
        var targetSuperAdminId = request.SuperAdminUserId ?? granterUserId;

        var command = new GrantBreakGlassAccessCommand(
            request.TargetProjectId,
            targetSuperAdminId,
            granterUserId,
            request.Reason,
            request.DurationMinutes ?? 60);

        var result = await grantAccessUseCase.ExecuteAsync(command, ct);
        return CreatedAtAction(
            nameof(ListGrants),
            new { targetProjectId = result.TargetProjectId },
            new ApiResponse<BreakGlassGrantResultDto>(result));
    }

    /// <summary>Revoke an active break-glass grant immediately with optimistic concurrency.</summary>
    [HttpDelete("{grantId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status412PreconditionFailed)]
    [ProducesResponseType(StatusCodes.Status428PreconditionRequired)]
    public async Task<IActionResult> RevokeAccess(
        [FromRoute] Guid grantId,
        [FromIfMatch] long expectedVersion,
        CancellationToken ct)
    {
        var command = new RevokeBreakGlassAccessCommand(grantId, expectedVersion);
        await revokeAccessUseCase.ExecuteAsync(command, ct);
        return NoContent();
    }
}
