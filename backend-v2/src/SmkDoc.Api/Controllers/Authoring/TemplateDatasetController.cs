using Microsoft.AspNetCore.Mvc;
using SmkDoc.Api.Common.Responses;
using SmkDoc.Api.Contracts.Authoring.Templates;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.Authoring.FieldMappings.Commands.SaveTemplateDatasets;
using SmkDoc.Application.Modules.Authoring.FieldMappings.DTOs;
using SmkDoc.Application.Modules.Authoring.FieldMappings.Queries.GetTemplateDatasets;

namespace SmkDoc.Api.Controllers.Authoring;

/// <summary>
/// Template Datasets Controller: link and unlink data queries to templates.
/// Auth: Channel A (X-API-Key via ApiKeyMiddleware) or Channel B (Bearer JWT).
/// </summary>
[ApiController]
[Route("api/v1/templates/{id:guid}/datasets")]
public sealed class TemplateDatasetController(
    IUseCase<GetTemplateDatasetsQuery, List<TemplateDatasetDto>> getDatasetsUseCase,
    IUseCase<SaveTemplateDatasetsCommand> saveDatasetsUseCase) : ControllerBase
{
    /// <summary>Get datasets linked to this template.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<IEnumerable<TemplateDatasetDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetDatasets([FromRoute] Guid id, CancellationToken ct)
    {
        var query = new GetTemplateDatasetsQuery(id);
        var result = await getDatasetsUseCase.ExecuteAsync(query, ct);
        return Ok(new ApiResponse<IEnumerable<TemplateDatasetDto>>(result));
    }

    /// <summary>Save (replace-all) dataset links for a template.</summary>
    [HttpPut]
    [ProducesResponseType(typeof(ApiResponse<IEnumerable<TemplateDatasetDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SaveDatasets(
        [FromRoute] Guid id,
        [FromBody] SaveTemplateDatasetsRequest request,
        CancellationToken ct)
    {
        var items = request.Datasets ?? [];
        var dtos = items.Select(i => new SaveTemplateDatasetItemDto(i.DatasetId, i.Alias, i.SortOrder)).ToList();
        var command = new SaveTemplateDatasetsCommand(id, dtos);
        await saveDatasetsUseCase.ExecuteAsync(command, ct);

        var query = new GetTemplateDatasetsQuery(id);
        var result = await getDatasetsUseCase.ExecuteAsync(query, ct);
        return Ok(new ApiResponse<IEnumerable<TemplateDatasetDto>>(result));
    }
}

