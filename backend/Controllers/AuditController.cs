using Microsoft.AspNetCore.Mvc;
using SmkDocServer.Domain.Interfaces;

namespace SmkDocServer.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuditController : ControllerBase
{
    private readonly IGenerationLogService _logService;
    private readonly ILogger<AuditController> _logger;

    public AuditController(IGenerationLogService logService, ILogger<AuditController> logger)
    {
        _logService = logService;
        _logger = logger;
    }

    [HttpGet("logs")]
    public async Task<IActionResult> GetLogs(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        [FromQuery] Guid? projectId = null,
        [FromQuery] string? status = null)
    {
        var (logs, totalCount) = await _logService.GetRecentLogsAsync(page, pageSize, projectId, status);
        return Ok(new
        {
            page,
            pageSize,
            totalCount,
            totalPages = (int)Math.Ceiling((double)totalCount / pageSize),
            logs
        });
    }

    [HttpGet("metrics")]
    public async Task<IActionResult> GetMetrics()
    {
        var metrics = await _logService.GetMetricsAsync();
        return Ok(metrics);
    }
}
