using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmkDoc.Api.Common.Responses;
using SmkDoc.Application.Modules.Rendering.Logs.DTOs;
using SmkDoc.Application.Modules.Rendering.Logs.Queries.GetLogMetrics;
using SmkDoc.Application.Modules.Rendering.Logs.Queries.ListGenerationLogs;

namespace SmkDoc.Api.Controllers.Rendering;

/// <summary>
/// Document generation audit logging and rendering performance metrics.
/// Auth: Channel B (Bearer JWT).
/// </summary>
[ApiController]
[Route("api/v1/logs")]
[Route("api/logs")]
[Authorize]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
public class AuditLogController(
    ListGenerationLogsUseCase listUseCase,
    GetLogMetricsUseCase metricsUseCase) : ControllerBase
{
    /// <summary>List paginated document generation audit logs.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedApiResponse<GenerationLogDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListLogs(
        [FromQuery] int page = 1,
        [FromQuery] int limit = 50,
        [FromQuery] string? app = null,
        CancellationToken ct = default)
    {
        var query = new ListGenerationLogsQuery(page, limit, app);
        var result = await listUseCase.ExecuteAsync(query, ct);
        var response = new PagedApiResponse<GenerationLogDto>(result.Logs, result.Total, result.Page, result.Limit);
        return Ok(response);
    }

    /// <summary>Get document generation aggregate performance metrics.</summary>
    [HttpGet("metrics")]
    [ProducesResponseType(typeof(ApiResponse<LogMetricsDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMetrics(
        [FromQuery] DateTimeOffset? startDate = null,
        [FromQuery] DateTimeOffset? endDate = null,
        CancellationToken ct = default)
    {
        var query = new GetLogMetricsQuery(startDate, endDate);
        var result = await metricsUseCase.ExecuteAsync(query, ct);
        return Ok(new ApiResponse<LogMetricsDto>(result));
    }
}
