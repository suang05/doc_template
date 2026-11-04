using Microsoft.AspNetCore.Mvc;
using SmkDoc.Api.Common.Responses;
using SmkDoc.Api.Contracts.Authoring.Templates;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.Authoring.Templates.Commands.RollbackTemplateVersion;
using SmkDoc.Application.Modules.Authoring.Templates.DTOs;
using SmkDoc.Application.Modules.Authoring.Templates.Queries.ListTemplateVersions;

namespace SmkDoc.Api.Controllers.Authoring;

/// <summary>
/// Template Versioning — list immutable versions and rollback to previous version.
/// Auth: Channel A (X-API-Key via ApiKeyMiddleware) or Channel B (Bearer JWT).
/// </summary>
[ApiController]
[Route("api/v1/templates/{id:guid}")]
public sealed class TemplateVersionController(
    IUseCase<ListTemplateVersionsQuery, List<TemplateVersionDto>> listVersionsUseCase,
    IUseCase<RollbackTemplateVersionCommand, int> rollbackUseCase) : ControllerBase
{
    /// <summary>List all versions of a template.</summary>
    [HttpGet("versions")]
    [ProducesResponseType(typeof(ApiResponse<IEnumerable<TemplateVersionDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetVersions([FromRoute] Guid id, CancellationToken ct)
    {
        var query = new ListTemplateVersionsQuery(id);
        var versions = await listVersionsUseCase.ExecuteAsync(query, ct);
        return Ok(new ApiResponse<IEnumerable<TemplateVersionDto>>(versions));
    }

    /// <summary>
    /// Rollback to a previous version — creates a new version with the old content.
    /// </summary>
    [HttpPost("rollback/{version:int}")]
    [ProducesResponseType(typeof(ApiResponse<RollbackVersionResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> RollbackVersion(
        [FromRoute] Guid id,
        [FromRoute] int version,
        CancellationToken ct)
    {
        var command = new RollbackTemplateVersionCommand(id, version);
        int newVersion = await rollbackUseCase.ExecuteAsync(command, ct);
        return Ok(new ApiResponse<RollbackVersionResponse>(new RollbackVersionResponse(newVersion)));
    }
}
