using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SmkDoc.Api.Common.Responses;
using SmkDoc.Api.Extensions;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.IdentityAccess.Projects.DTOs;
using SmkDoc.Application.Modules.IdentityAccess.Projects.Queries.ListProjects;

namespace SmkDoc.Api.Controllers.IdentityAccess;

/// <summary>
/// Authenticated portal user workspace directory listing only projects where the caller
/// holds explicit membership or an active Break-Glass grant (Zero Standing Access).
/// </summary>
[ApiController]
[Route("api/v1/portal/projects")]
[Authorize]
[EnableRateLimiting("api")]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
public sealed class PortalProjectsController(
    IUseCase<ListProjectsQuery, ProjectPagedResultDto> listProjectsUseCase) : ControllerBase
{
    /// <summary>List projects accessible to the authenticated portal user.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedApiResponse<ProjectResultDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListAccessibleProjects(
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int limit = 20,
        [FromQuery] bool includeTotal = false,
        CancellationToken ct = default)
    {
        var userId = User.GetUserId();
        var query = new ListProjectsQuery(userId, search, page, limit, includeTotal);
        var result = await listProjectsUseCase.ExecuteAsync(query, ct);
        return Ok(new PagedApiResponse<ProjectResultDto>(result.Items, result.TotalCount, result.Page, result.Limit));
    }
}
