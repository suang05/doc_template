using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmkDoc.Api.Models;
using SmkDoc.Application.DTOs.Templates;
using SmkDoc.Application.DTOs.FieldMappings;
using SmkDoc.Application.UseCases.FieldMappings;
using SmkDoc.Application.UseCases.Templates;


namespace SmkDoc.Api.Controllers;

/// <summary>
/// Template Versioning — list versions, rollback, and link/unlink datasets.
/// Auth: X-API-Key via ApiKeyMiddleware.
/// </summary>
[ApiController]
[Route("api/v1/templates/{id:guid}")]
[Authorize(Policy = "ApiKeyPolicy")]
public class TemplateVersionController(
    TemplateManagementUseCase templateUseCase,
    TemplateDatasetUseCase templateDatasetUseCase) : ControllerBase
{
    // ── Versioning ────────────────────────────────────────────────────────────

    /// <summary>List all versions of a template.</summary>
    [HttpGet("versions")]
    [ProducesResponseType(typeof(ApiResponse<IEnumerable<TemplateVersionDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetVersions([FromRoute] Guid id, CancellationToken ct)
    {
        var versions = await templateUseCase.ListVersionsAsync(id, ct);
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
        int newVersion = await templateUseCase.RollbackVersionAsync(id, version, ct);
        return Ok(new ApiResponse<RollbackVersionResponse>(new RollbackVersionResponse(newVersion)));
    }

    // ── Datasets ──────────────────────────────────────────────────────────────

    /// <summary>Get datasets linked to this template.</summary>
    [HttpGet("datasets")]
    [ProducesResponseType(typeof(ApiResponse<IEnumerable<TemplateDatasetDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetDatasets([FromRoute] Guid id, CancellationToken ct)
    {
        var datasets = await templateDatasetUseCase.GetByTemplateIdAsync(id, ct);
        return Ok(new ApiResponse<IEnumerable<TemplateDatasetDto>>(datasets));
    }

    /// <summary>Save (replace-all) dataset links for a template.</summary>
    [HttpPut("datasets")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> SaveDatasets(
        [FromRoute] Guid id,
        [FromBody] List<SaveTemplateDatasetItemDto> items,
        CancellationToken ct)
    {
        await templateDatasetUseCase.SaveAsync(id, items, ct);
        return NoContent();
    }
}
