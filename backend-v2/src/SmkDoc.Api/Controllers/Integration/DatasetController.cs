using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmkDoc.Api.Common.Responses;
using SmkDoc.Api.Contracts.Integration.Datasets;
using SmkDoc.Application.Modules.Integration.Datasets.Commands.CreateDataset;
using SmkDoc.Application.Modules.Integration.Datasets.Commands.DeleteDataset;
using SmkDoc.Application.Modules.Integration.Datasets.Commands.UpdateDataset;
using SmkDoc.Application.Modules.Integration.Datasets.DTOs;
using SmkDoc.Application.Modules.Integration.Datasets.Queries.GetDatasetById;
using SmkDoc.Application.Modules.Integration.Datasets.Queries.ListDatasets;

namespace SmkDoc.Api.Controllers.Integration;

/// <summary>
/// SQL dataset definitions for query execution and template data binding.
/// Auth: Channel B (Bearer JWT).
/// </summary>
[ApiController]
[Route("api/v1/datasets")]
[Route("api/datasets")]
[Authorize]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
public class DatasetController(
    ListDatasetsUseCase listUseCase,
    GetDatasetByIdUseCase getByIdUseCase,
    CreateDatasetUseCase createUseCase,
    UpdateDatasetUseCase updateUseCase,
    DeleteDatasetUseCase deleteUseCase) : ControllerBase
{
    /// <summary>List all configured SQL datasets.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<IEnumerable<DatasetDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(CancellationToken ct)
    {
        var result = await listUseCase.ExecuteAsync(new ListDatasetsQuery(), ct);
        return Ok(new ApiResponse<IEnumerable<DatasetDto>>(result));
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<DatasetDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var result = await getByIdUseCase.ExecuteAsync(new GetDatasetByIdQuery(id), ct);
        if (result == null) return NotFound();
        return Ok(new ApiResponse<DatasetDto>(result));
    }

    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<DatasetDto>), StatusCodes.Status201Created)]
    public async Task<IActionResult> Create([FromBody] CreateDatasetRequest request, CancellationToken ct)
    {
        var command = new CreateDatasetCommand(request.Name, request.Description, request.DataConnectionId, request.SqlQuery, request.CacheSeconds);
        var result = await createUseCase.ExecuteAsync(command, ct);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, new ApiResponse<DatasetDto>(result));
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<DatasetDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateDatasetRequest request, CancellationToken ct)
    {
        var command = new UpdateDatasetCommand(id, request.Name, request.Description, request.DataConnectionId, request.SqlQuery, request.CacheSeconds);
        var result = await updateUseCase.ExecuteAsync(command, ct);
        if (result == null) return NotFound();
        return Ok(new ApiResponse<DatasetDto>(result));
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var success = await deleteUseCase.ExecuteAsync(new DeleteDatasetCommand(id), ct);
        if (!success) return NotFound();
        return NoContent();
    }
}
