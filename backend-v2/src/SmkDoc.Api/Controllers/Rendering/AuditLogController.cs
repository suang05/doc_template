using Microsoft.AspNetCore.Mvc;
using SmkDoc.Application.Modules.Rendering.Logs.DTOs;
using SmkDoc.Application.Modules.Rendering.Logs.Queries.GetLogMetrics;
using SmkDoc.Application.Modules.Rendering.Logs.Queries.ListGenerationLogs;

namespace SmkDoc.Api.Controllers;

[ApiController]
[Route("api/logs")]
public class AuditLogController(
    ListGenerationLogsUseCase listUseCase,
    GetLogMetricsUseCase metricsUseCase) : ControllerBase
{
    private readonly ListGenerationLogsUseCase _listUseCase = listUseCase;
    private readonly GetLogMetricsUseCase _metricsUseCase = metricsUseCase;

    [HttpGet]
    [ProducesResponseType(typeof(GenerationLogPagedResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListLogs(
        [FromQuery] int page = 1,
        [FromQuery] int limit = 50,
        [FromQuery] string? app = null,
        CancellationToken ct = default)
    {
        var result = await _listUseCase.ExecuteAsync(new ListGenerationLogsQuery(page, limit, app), ct);
        return Ok(result);
    }

    [HttpGet("metrics")]
    [ProducesResponseType(typeof(LogMetricsDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMetrics(
        [FromQuery] DateTimeOffset? startDate = null,
        [FromQuery] DateTimeOffset? endDate = null,
        CancellationToken ct = default)
    {
        var result = await _metricsUseCase.ExecuteAsync(new GetLogMetricsQuery(startDate, endDate), ct);
        return Ok(result);
    }
}
