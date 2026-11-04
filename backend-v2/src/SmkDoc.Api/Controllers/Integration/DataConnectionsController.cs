using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SmkDoc.Api.Common.Responses;
using SmkDoc.Api.Constants;
using SmkDoc.Api.Contracts.Integration.DataConnections;
using SmkDoc.Application.Common.Interfaces;
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
/// Auth: Admin or SuperAdmin via SmkDocPolicies.CanManageDataConnections.
/// </summary>
[ApiController]
[Route("api/v1/data-connections")]
[Authorize(Policy = SmkDocPolicies.CanManageDataConnections)]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
[ProducesResponseType(StatusCodes.Status403Forbidden)]
public sealed class DataConnectionsController(
    IUseCase<ListDataConnectionsQuery, List<DataConnectionDto>> listUseCase,
    IUseCase<GetDataConnectionByIdQuery, DataConnectionDto> getByIdUseCase,
    IUseCase<CreateDataConnectionCommand, DataConnectionDto> createUseCase,
    IUseCase<UpdateDataConnectionCommand, DataConnectionDto> updateUseCase,
    IUseCase<DeleteDataConnectionCommand, bool> deleteUseCase,
    IUseCase<TestDataConnectionCommand, TestDataConnectionResultDto> testUseCase) : ControllerBase
{
    /// <summary>List all configured database connections.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<IEnumerable<DataConnectionDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(CancellationToken ct)
    {
        var query = new ListDataConnectionsQuery();
        var result = await listUseCase.ExecuteAsync(query, ct);
        return Ok(new ApiResponse<IEnumerable<DataConnectionDto>>(result));
    }

    /// <summary>Get a specific database connection definition by its ID.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<DataConnectionDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById([FromRoute] Guid id, CancellationToken ct)
    {
        var query = new GetDataConnectionByIdQuery(id);
        var result = await getByIdUseCase.ExecuteAsync(query, ct);
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
    [ProducesResponseType(StatusCodes.Status412PreconditionFailed)]
    public async Task<IActionResult> Update(
        [FromRoute] Guid id,
        [FromBody] UpdateDataConnectionRequest request,
        [FromHeader(Name = "If-Match")] string? ifMatch,
        CancellationToken ct)
    {
        var command = new UpdateDataConnectionCommand(id, request.Name, request.Provider, request.ConnectionString);
        var result = await updateUseCase.ExecuteAsync(command, ct);
        return Ok(new ApiResponse<DataConnectionDto>(result));
    }

    /// <summary>Delete a database connection definition.</summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status412PreconditionFailed)]
    public async Task<IActionResult> Delete(
        [FromRoute] Guid id,
        [FromHeader(Name = "If-Match")] string? ifMatch,
        CancellationToken ct)
    {
        var command = new DeleteDataConnectionCommand(id);
        await deleteUseCase.ExecuteAsync(command, ct);
        return NoContent();
    }

    /// <summary>Test connection credentials and reachability.</summary>
    [HttpPost("test")]
    [EnableRateLimiting("test-conn")]
    [ProducesResponseType(typeof(ApiResponse<TestDataConnectionResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> TestConnection([FromBody] TestDataConnectionRequest request, CancellationToken ct)
    {
        var command = new TestDataConnectionCommand(request.Provider, request.ConnectionString);
        var result = await testUseCase.ExecuteAsync(command, ct);
        return Ok(new ApiResponse<TestDataConnectionResultDto>(result));
    }
}
