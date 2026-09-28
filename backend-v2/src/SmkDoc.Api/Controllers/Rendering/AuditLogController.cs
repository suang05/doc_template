using Microsoft.AspNetCore.Mvc;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.Rendering.Logs;
using SmkDoc.Domain.Entities;

namespace SmkDoc.Api.Controllers;

[ApiController]
[Route("api/logs")]
public class AuditLogController : ControllerBase
{
    private readonly GenerationLogUseCase _logUseCase;

    public AuditLogController(GenerationLogUseCase logUseCase)
    {
        _logUseCase = logUseCase;
    }

    [HttpGet]
    public async Task<IActionResult> ListLogs(
        [FromQuery] int page = 1,
        [FromQuery] int limit = 50,
        [FromQuery] string? app = null,
        CancellationToken ct = default)
    {
        var result = await _logUseCase.ListAsync(page, limit, app, ct);
        return Ok(result);
    }
}
