using Microsoft.AspNetCore.Mvc;
using SmkDoc.Api.Binders;
using SmkDoc.Api.Common.Responses;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.Authoring.Templates.Commands.RollbackTemplateVersion;
using SmkDoc.Application.Modules.Authoring.Templates.DTOs;
using SmkDoc.Application.Modules.Authoring.Templates.Queries.ListTemplateVersions;

namespace SmkDoc.Api.Controllers.Authoring;

/// <summary>
/// Template Versioning — list immutable versions and rollback to previous version.
/// Auth: Channel A (Authorization: ApiKey) or Channel B (Portal Bearer JWT) via ProjectContextMiddleware.
/// </summary>
[ApiController]
[Route("api/v1/templates/{id:guid}")]
public sealed class TemplateVersionController(
    IUseCase<ListTemplateVersionsQuery, List<TemplateVersionDto>> listVersionsUseCase,
    IUseCase<RollbackTemplateVersionCommand, RollbackVersionResultDto> rollbackUseCase) : ControllerBase
{
    /// <summary>List all versions of a template.</summary>
    [HttpGet("versions")]
    [ProducesResponseType(typeof(ApiResponse<IEnumerable<TemplateVersionDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetVersions([FromRoute] Guid id, CancellationToken ct)
    {
        var query = new ListTemplateVersionsQuery(id);
        var result = await listVersionsUseCase.ExecuteAsync(query, ct);
        return Ok(new ApiResponse<IEnumerable<TemplateVersionDto>>(result));
    }

    /// <summary>
    /// Rollback to a previous version — creates a new version with the old content.
    /// </summary>
    [HttpPost("rollback/{version:int}")]
    [ProducesResponseType(typeof(ApiResponse<RollbackVersionResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status412PreconditionFailed)]
    [ProducesResponseType(StatusCodes.Status428PreconditionRequired)]
    public async Task<IActionResult> RollbackVersion(
        [FromRoute] Guid id,
        [FromRoute] int version,
        [FromIfMatch] long expectedVersion,
        CancellationToken ct)
    {
        var command = new RollbackTemplateVersionCommand(id, version, expectedVersion);
        var result = await rollbackUseCase.ExecuteAsync(command, ct);
        return Ok(new ApiResponse<RollbackVersionResultDto>(result));
    }
}
