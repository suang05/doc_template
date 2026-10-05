using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmkDoc.Api.Common.Responses;
using SmkDoc.Api.Contracts.Integration.DataConnections;
using SmkDoc.Application.Modules.Integration.DataConnections.Commands.CreateDataConnection;
using SmkDoc.Application.Modules.Integration.DataConnections.Commands.DeleteDataConnection;
using SmkDoc.Application.Modules.Integration.DataConnections.Commands.TestDataConnection;
using SmkDoc.Application.Modules.Integration.DataConnections.Commands.UpdateDataConnection;
using SmkDoc.Application.Modules.Integration.DataConnections.DTOs;
using SmkDoc.Application.Modules.Integration.DataConnections.Queries.GetDataConnectionById;
using SmkDoc.Application.Modules.Integration.DataConnections.Queries.ListDataConnections;

namespace SmkDoc.Api.Controllers.Integration;

/// <summary>
/// Database connection definitions for dynamic template data binding.
/// Auth: Channel B (Bearer JWT).
/// </summary>
[ApiController]
[Route("api/v1/data-connections")]
[Route("api/data-connections")]
[Authorize]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
public class DataConnectionsController(
    ListDataConnectionsUseCase listUseCase,
    GetDataConnectionByIdUseCase getByIdUseCase,
    CreateDataConnectionUseCase createUseCase,
    UpdateDataConnectionUseCase updateUseCase,
    DeleteDataConnectionUseCase deleteUseCase,
    TestDataConnectionUseCase testUseCase) : ControllerBase
{
    /// <summary>List all configured database connections.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<IEnumerable<DataConnectionDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(CancellationToken ct)
    {
        var result = await listUseCase.ExecuteAsync(new ListDataConnectionsQuery(), ct);
        return Ok(new ApiResponse<IEnumerable<DataConnectionDto>>(result));
    }

    /// <summary>Get a specific database connection definition by its ID.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<DataConnectionDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var result = await getByIdUseCase.ExecuteAsync(new GetDataConnectionByIdQuery(id), ct);
        if (result == null) return NotFound();
        return Ok(new ApiResponse<DataConnectionDto>(result));
    }

    /// <summary>Create a new database connection definition.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<DataConnectionDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateDataConnectionRequest request, CancellationToken ct)
    {
        var command = new CreateDataConnectionCommand(request.Name, request.Provider, request.ConnectionString);
        var result = await createUseCase.ExecuteAsync(command, ct);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, new ApiResponse<DataConnectionDto>(result));
    }

    /// <summary>Update an existing database connection definition.</summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<DataConnectionDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateDataConnectionRequest request, CancellationToken ct)
    {
        var command = new UpdateDataConnectionCommand(id, request.Name, request.Provider, request.ConnectionString);
        var result = await updateUseCase.ExecuteAsync(command, ct);
        if (result == null) return NotFound();
        return Ok(new ApiResponse<DataConnectionDto>(result));
    }

    /// <summary>Delete a database connection definition.</summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var success = await deleteUseCase.ExecuteAsync(new DeleteDataConnectionCommand(id), ct);
        if (!success) return NotFound();
        return NoContent();
    }

    /// <summary>Test connection credentials and reachability.</summary>
    [HttpPost("test")]
    [ProducesResponseType(typeof(ApiResponse<TestDataConnectionResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<TestDataConnectionResponseDto>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> TestConnection([FromBody] TestDataConnectionRequest request, CancellationToken ct)
    {
        var command = new TestDataConnectionCommand(request.Provider, request.ConnectionString);
        var success = await testUseCase.ExecuteAsync(command, ct);
        if (success)
            return Ok(new ApiResponse<TestDataConnectionResponseDto>(new TestDataConnectionResponseDto(true, "Connection successful.")));

        return BadRequest(new ApiResponse<TestDataConnectionResponseDto>(new TestDataConnectionResponseDto(false, null, "Connection failed.")));
    }
}
