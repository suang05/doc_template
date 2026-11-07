using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmkDoc.Api.Common.Responses;
using SmkDoc.Api.Constants;
using SmkDoc.Api.Contracts.Integration.Datasets;
using SmkDoc.Api.Filters;
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
/// Auth: Author, Developer, Admin, SuperAdmin, or M2M ReadWrite scope via AuthorizationPolicies.CanManageDatasets.
/// </summary>
[ApiController]
[Route("api/v1/datasets")]
[Authorize(Policy = AuthorizationPolicies.CanManageDatasets)]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
[ProducesResponseType(StatusCodes.Status403Forbidden)]
public sealed class DatasetsController(
    IUseCase<ListDatasetsQuery, List<DatasetResultDto>> listUseCase,
    IUseCase<GetDatasetByIdQuery, DatasetResultDto> getByIdUseCase,
    IUseCase<CreateDatasetCommand, DatasetResultDto> createUseCase,
    IUseCase<UpdateDatasetCommand, DatasetResultDto> updateUseCase,
    IUseCase<DeleteDatasetCommand, bool> deleteUseCase) : ControllerBase
{
    /// <summary>List all configured SQL datasets.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<IEnumerable<DatasetResultDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(CancellationToken ct)
    {
        var query = new ListDatasetsQuery();
        var result = await listUseCase.ExecuteAsync(query, ct);
        return Ok(new ApiResponse<IEnumerable<DatasetResultDto>>(result));
    }

    /// <summary>Get a specific SQL dataset definition by its ID.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<DatasetResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById([FromRoute] Guid id, CancellationToken ct)
    {
        var query = new GetDatasetByIdQuery(id);
        var result = await getByIdUseCase.ExecuteAsync(query, ct);
        Response.Headers.ETag = $"\"{result.ConcurrencyToken}\"";
        return Ok(new ApiResponse<DatasetResultDto>(result));
    }

    /// <summary>Create a new SQL dataset definition.</summary>
    [HttpPost]
    [Idempotent]
    [ProducesResponseType(typeof(ApiResponse<DatasetResultDto>), StatusCodes.Status201Created)]
    public async Task<IActionResult> Create([FromBody] CreateDatasetRequest request, CancellationToken ct)
    {
        var command = new CreateDatasetCommand(request.Name, request.Description, request.DataConnectionId, request.SqlQuery, request.CacheSeconds);
        var result = await createUseCase.ExecuteAsync(command, ct);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, new ApiResponse<DatasetResultDto>(result));
    }

    /// <summary>Update an existing SQL dataset definition.</summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<DatasetResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status412PreconditionFailed)]
    public async Task<IActionResult> Update(
        [FromRoute] Guid id,
        [FromBody] UpdateDatasetRequest request,
        [FromHeader(Name = "If-Match")] string? ifMatch,
        CancellationToken ct)
    {
        var command = new UpdateDatasetCommand(id, request.Name, request.Description, request.DataConnectionId, request.SqlQuery, request.CacheSeconds, ifMatch);
        var result = await updateUseCase.ExecuteAsync(command, ct);
        Response.Headers.ETag = $"\"{result.ConcurrencyToken}\"";
        return Ok(new ApiResponse<DatasetResultDto>(result));
    }

    /// <summary>Delete a SQL dataset definition.</summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status412PreconditionFailed)]
    public async Task<IActionResult> Delete(
        [FromRoute] Guid id,
        [FromHeader(Name = "If-Match")] string? ifMatch,
        CancellationToken ct)
    {
        var command = new DeleteDatasetCommand(id, ifMatch);
        await deleteUseCase.ExecuteAsync(command, ct);
        return NoContent();
    }
}

