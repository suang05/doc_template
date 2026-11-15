using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SmkDoc.Api.Common.Responses;
using SmkDoc.Api.Constants;
using SmkDoc.Api.Contracts.Integration.DataConnections;
using SmkDoc.Api.Filters;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.Integration.DataConnections.Commands.CheckSavedDataConnection;
using SmkDoc.Application.Modules.Integration.DataConnections.Commands.TestDataConnection;
using SmkDoc.Application.Modules.Integration.DataConnections.DTOs;

namespace SmkDoc.Api.Controllers.Integration;

/// <summary>
/// Operational diagnostics and reachability tests for database connections.
/// Auth: Admin or SuperAdmin via AuthorizationPolicies.CanManageDataConnections.
/// </summary>
[ApiController]
[Route("api/v1/data-connections")]
[Authorize(Policy = AuthorizationPolicies.CanManageDataConnections)]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
[ProducesResponseType(StatusCodes.Status403Forbidden)]
public sealed class DataConnectionDiagnosticsController(
    IUseCase<TestDataConnectionCommand, TestDataConnectionResultDto> testUseCase,
    IUseCase<CheckSavedDataConnectionCommand, TestDataConnectionResultDto> checkSavedUseCase) : ControllerBase
{
    /// <summary>Check reachability and credentials for an already-saved database connection.</summary>
    [HttpPost("{id:guid}/connectivity-checks")]
    [EnableRateLimiting("connectivity-check")]
    [ProducesResponseType(typeof(ApiResponse<TestDataConnectionResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CheckSavedConnection([FromRoute] Guid id, CancellationToken ct)
    {
        var command = new CheckSavedDataConnectionCommand(id);
        var result = await checkSavedUseCase.ExecuteAsync(command, ct);
        return Ok(new ApiResponse<TestDataConnectionResultDto>(result));
    }

    /// <summary>Check reachability and credentials for a supplied connection specification.</summary>
    [HttpPost("connectivity-checks")]
    [EnableRateLimiting("connectivity-check")]
    [ProducesResponseType(typeof(ApiResponse<TestDataConnectionResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public Task<IActionResult> CheckConnection([FromBody] TestDataConnectionRequest request, CancellationToken ct) =>
        ExecuteConnectivityCheckAsync(request, ct);

    /// <summary>Legacy connectivity check alias. Use POST /api/v1/data-connections/connectivity-checks instead.</summary>
    [HttpPost("test")]
    [DeprecatedEndpoint]
    [EnableRateLimiting("test-conn")]
    [ProducesResponseType(typeof(ApiResponse<TestDataConnectionResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public Task<IActionResult> TestConnection([FromBody] TestDataConnectionRequest request, CancellationToken ct) =>
        ExecuteConnectivityCheckAsync(request, ct);

    private async Task<IActionResult> ExecuteConnectivityCheckAsync(TestDataConnectionRequest request, CancellationToken ct)
    {
        var command = new TestDataConnectionCommand(request.Provider, request.ConnectionString);
        var result = await testUseCase.ExecuteAsync(command, ct);
        return Ok(new ApiResponse<TestDataConnectionResultDto>(result));
    }
}

