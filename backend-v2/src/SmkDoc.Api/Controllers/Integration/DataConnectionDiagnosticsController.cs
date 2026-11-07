using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SmkDoc.Api.Common.Responses;
using SmkDoc.Api.Constants;
using SmkDoc.Api.Contracts.Integration.DataConnections;
using SmkDoc.Application.Common.Interfaces;
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
    IUseCase<TestDataConnectionCommand, TestDataConnectionResultDto> testUseCase) : ControllerBase
{
    /// <summary>Test connection credentials and reachability.</summary>
    [HttpPost("test")]
    [EnableRateLimiting("test-conn")]
    [ProducesResponseType(typeof(ApiResponse<TestDataConnectionResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> TestConnection([FromBody] TestDataConnectionRequest request, CancellationToken ct)
    {
        var command = new TestDataConnectionCommand(request.Provider, request.ConnectionString);
        var result = await testUseCase.ExecuteAsync(command, ct);
        return Ok(new ApiResponse<TestDataConnectionResultDto>(result));
    }
}

