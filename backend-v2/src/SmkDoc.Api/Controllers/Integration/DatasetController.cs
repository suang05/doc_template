using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmkDoc.Api.Common.Responses;
using SmkDoc.Api.Constants;
using SmkDoc.Api.Contracts.Integration.Datasets;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.Integration.Datasets.Commands.CreateDataset;
using SmkDoc.Application.Modules.Integration.Datasets.Commands.DeleteDataset;
using SmkDoc.Application.Modules.Integration.Datasets.Commands.UpdateDataset;
using SmkDoc.Application.Modules.Integration.Datasets.DTOs;
using SmkDoc.Application.Modules.Integration.Datasets.Queries.GetDatasetById;
using SmkDoc.Application.Modules.Integration.Datasets.Queries.ListDatasets;

namespace SmkDoc.Api.Controllers.Integration;

/// <summary>
/// SQL dataset definitions for query execution and template data binding.
/// Auth: Author, Developer, Admin, SuperAdmin, or M2M ReadWrite scope via SmkDocPolicies.CanManageDatasets.
/// </summary>
[ApiController]
[Route("api/v1/datasets")]
[Authorize(Policy = SmkDocPolicies.CanManageDatasets)]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
[ProducesResponseType(StatusCodes.Status403Forbidden)]
public sealed class DatasetController(
    IUseCase<ListDatasetsQuery, List<DatasetDto>> listUseCase,
    IUseCase<GetDatasetByIdQuery, DatasetDto?> getByIdUseCase,
    IUseCase<CreateDatasetCommand, DatasetDto> createUseCase,
    IUseCase<UpdateDatasetCommand, DatasetDto?> updateUseCase,
    IUseCase<DeleteDatasetCommand, bool> deleteUseCase) : ControllerBase
{
    /// <summary>List all configured SQL datasets.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<IEnumerable<DatasetDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(CancellationToken ct)
    {
        var query = new ListDatasetsQuery();
        var result = await listUseCase.ExecuteAsync(query, ct);
        return Ok(new ApiResponse<IEnumerable<DatasetDto>>(result));
    }

    /// <summary>Get a specific SQL dataset definition by its ID.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<DatasetDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var query = new GetDatasetByIdQuery(id);
        var result = await getByIdUseCase.ExecuteAsync(query, ct);
        if (result == null) return NotFound();
        return Ok(new ApiResponse<DatasetDto>(result));
    }

    /// <summary>Create a new SQL dataset definition.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<DatasetDto>), StatusCodes.Status201Created)]
    public async Task<IActionResult> Create([FromBody] CreateDatasetRequest request, CancellationToken ct)
    {
        var command = new CreateDatasetCommand(request.Name, request.Description, request.DataConnectionId, request.SqlQuery, request.CacheSeconds);
        var result = await createUseCase.ExecuteAsync(command, ct);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, new ApiResponse<DatasetDto>(result));
    }

    /// <summary>Update an existing SQL dataset definition.</summary>
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

    /// <summary>Delete a SQL dataset definition.</summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var command = new DeleteDatasetCommand(id);
        var success = await deleteUseCase.ExecuteAsync(command, ct);
        if (!success) return NotFound();
        return NoContent();
    }
}
