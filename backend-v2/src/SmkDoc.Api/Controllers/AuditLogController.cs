using Microsoft.AspNetCore.Mvc;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Domain.Entities;

namespace SmkDoc.Api.Controllers;

[ApiController]
[Route("api/logs")]
public class AuditLogController : ControllerBase
{
    private readonly IRepository<GenerationLog> _logRepo;

    public AuditLogController(IRepository<GenerationLog> logRepo)
    {
        _logRepo = logRepo;
    }

    [HttpGet]
    public async Task<IActionResult> ListLogs(
        [FromQuery] int page = 1,
        [FromQuery] int limit = 50,
        [FromQuery] string? app = null,
        CancellationToken ct = default)
    {
        page  = Math.Max(1, page);
        limit = Math.Clamp(limit, 1, 200);

        var (logs, total) = await _logRepo.PagedListAsync(
            predicate:  string.IsNullOrWhiteSpace(app) ? null : l => l.CallerApp == app,
            orderBy:    l => l.CreatedAt,
            descending: true,
            page:       page,
            limit:      limit,
            ct:         ct);

        return Ok(new { logs, total, page, limit });
    }
}
