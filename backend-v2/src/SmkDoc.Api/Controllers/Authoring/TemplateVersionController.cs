using Microsoft.AspNetCore.Mvc;
using SmkDoc.Api.Common.Responses;
using SmkDoc.Api.Contracts.Authoring.Templates;
using SmkDoc.Application.Modules.Authoring.Templates.DTOs;
using SmkDoc.Application.Modules.Authoring.FieldMappings.DTOs;
using SmkDoc.Application.Modules.Authoring.FieldMappings.Queries.GetTemplateDatasets;
using SmkDoc.Application.Modules.Authoring.FieldMappings.Commands.SaveTemplateDatasets;
using SmkDoc.Application.Modules.Authoring.Templates.Queries.ListTemplateVersions;
using SmkDoc.Application.Modules.Authoring.Templates.Commands.RollbackTemplateVersion;

namespace SmkDoc.Api.Controllers.Authoring;

/// <summary>
/// Template Versioning — list versions, rollback, and link/unlink datasets.
/// Auth: X-API-Key via ApiKeyMiddleware.
/// </summary>
[ApiController]
[Route("api/v1/templates/{id:guid}")]
[Route("api/templates/{id:guid}")]
public class TemplateVersionController(
    ListTemplateVersionsUseCase listVersionsUseCase,
    RollbackTemplateVersionUseCase rollbackUseCase,
    GetTemplateDatasetsUseCase getDatasetsUseCase,
    SaveTemplateDatasetsUseCase saveDatasetsUseCase) : ControllerBase
{
    // ── Versioning ────────────────────────────────────────────────────────────

    /// <summary>List all versions of a template.</summary>
    [HttpGet("versions")]
    [ProducesResponseType(typeof(ApiResponse<IEnumerable<TemplateVersionDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetVersions([FromRoute] Guid id, CancellationToken ct)
    {
        var versions = await listVersionsUseCase.ExecuteAsync(new ListTemplateVersionsQuery(id), ct);
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
        int newVersion = await rollbackUseCase.ExecuteAsync(new RollbackTemplateVersionCommand(id, version), ct);
        return Ok(new ApiResponse<RollbackVersionResponse>(new RollbackVersionResponse(newVersion)));
    }

    // ── Datasets ──────────────────────────────────────────────────────────────

    /// <summary>Get datasets linked to this template.</summary>
    [HttpGet("datasets")]
    [ProducesResponseType(typeof(ApiResponse<IEnumerable<TemplateDatasetDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetDatasets([FromRoute] Guid id, CancellationToken ct)
    {
        var datasets = await getDatasetsUseCase.ExecuteAsync(new GetTemplateDatasetsQuery(id), ct);
        return Ok(new ApiResponse<IEnumerable<TemplateDatasetDto>>(datasets));
    }

    /// <summary>Save (replace-all) dataset links for a template.</summary>
    [HttpPut("datasets")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> SaveDatasets(
        [FromRoute] Guid id,
        [FromBody] List<SaveTemplateDatasetItemRequest> items,
        CancellationToken ct)
    {
        var dtos = items.Select(i => new SaveTemplateDatasetItemDto(i.DatasetId, i.Alias, i.SortOrder)).ToList();
        await saveDatasetsUseCase.ExecuteAsync(new SaveTemplateDatasetsCommand(id, dtos), ct);
        return Ok(new ApiResponse<object>(new { success = true }));
    }
}
