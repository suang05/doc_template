using Microsoft.AspNetCore.Mvc;
using SmkDoc.Api.Common.Responses;
using SmkDoc.Application.Modules.Rendering.Logs.DTOs;
using SmkDoc.Application.Modules.Rendering.Logs.Queries.GetLogMetrics;
using SmkDoc.Application.Modules.Rendering.Logs.Queries.ListGenerationLogs;

namespace SmkDoc.Api.Controllers.Rendering;

[ApiController]
[Route("api/logs")]
public class AuditLogController(
    ListGenerationLogsUseCase listUseCase,
    GetLogMetricsUseCase metricsUseCase) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<GenerationLogPagedResultDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListLogs(
        [FromQuery] int page = 1,
        [FromQuery] int limit = 50,
        [FromQuery] string? app = null,
        CancellationToken ct = default)
    {
        var result = await listUseCase.ExecuteAsync(new ListGenerationLogsQuery(page, limit, app), ct);
        return Ok(new ApiResponse<GenerationLogPagedResultDto>(result));
    }

    [HttpGet("metrics")]
    [ProducesResponseType(typeof(ApiResponse<LogMetricsDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMetrics(
        [FromQuery] DateTimeOffset? startDate = null,
        [FromQuery] DateTimeOffset? endDate = null,
        CancellationToken ct = default)
    {
        var result = await metricsUseCase.ExecuteAsync(new GetLogMetricsQuery(startDate, endDate), ct);
        return Ok(new ApiResponse<LogMetricsDto>(result));
    }
}
